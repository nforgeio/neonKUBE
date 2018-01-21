//-----------------------------------------------------------------------------
// FILE:	    EntityDatabase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.DynamicData;

using Couchbase.Lite.Store;

namespace Couchbase.Lite
{
    /// <summary>
    /// Adds type-safe entity functionality to the standard Couchbase Lite <see cref="Lite.Database"/>
    /// by wrapping the underling class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use the static <see cref="From(Database)"/> method to associate a new <see cref="EntityDatabase"/> 
    /// instance with an existing Couchbase Lite <see cref="Database"/>.  Be sure to call <see cref="Dispose"/> 
    /// when you're finished with the database to release resources associated with both the entity 
    /// and underlying databases.
    /// </para>
    /// <para>
    /// This class has two sets of methods for managing documents.  The first set creates or retrieves
    /// basic documents that wrap an entity type: <see cref="EntityDocument{TEntity}"/>, where these
    /// entity types were created by the Neon <b>entity-gen</b> code generator.  The relevant methods
    /// are <see cref="CreateEntityDocument()"/> , <see cref="GetEntityDocument(string)"/>, and 
    /// <see cref="GetExistingEntityDocument{TEntity}(string)"/>.  These work much like the corresponding
    /// base Couchbase Lite database methods.
    /// </para>
    /// <para>
    /// The second set of methods are used to create or retrieve binder documents.  These types derive 
    /// from <see cref="EntityDocument{TEntity}"/>, to implement <see cref="INotifyPropertyChanged"/> 
    /// behaviors for document attachments.  The relevant methods are <see cref="CreateBinderDocument{TDocument}"/>, 
    /// <see cref="GetBinderDocument{TDocument}(string)"/>, and <see cref="GetExistingBinderDocument{TDocument}(string)"/>.
    /// </para>
    /// </remarks>
    public class EntityDatabase : IDynamicEntityContext, IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        internal const string NotCompatibleError = "Neon Couchbase Lite extension doesn't support the current Couchbase Lite file system layout.  Please submit an issue to: https://github.com/neonFORGE/issues";

        private static object syncRoot = new object();

        /// <summary>
        /// Maps names to entity databases.
        /// </summary>
        private static Dictionary<string, EntityDatabase> nameToDatabase 
            = new Dictionary<string, EntityDatabase>();

        /// <summary>
        /// Maps a binder (derived) document type to information necessary to instantiate 
        /// the document (note: binder documents derive from [EntityDocument{TEntity}]
        /// to implement attachment properties.
        /// </summary>
        private static ConcurrentDictionary<Type, DerivedCreateInfo> typeToDerivedCreateInfo
            = new ConcurrentDictionary<System.Type, DerivedCreateInfo>();

        /// <summary>
        /// Returns the <see cref="EntityDatabase"/> wrapping the <see cref="Lite.Database"/> passed.
        /// </summary>
        /// <param name="database">The underlying database.</param>
        /// <returns>The <see cref="EntityDatabase"/>.</returns>
        internal static EntityDatabase From(Database database)
        {
            Covenant.Requires<ArgumentNullException>(database != null);

            lock (syncRoot)
            {
                EntityDatabase entityDatabase;

                if (nameToDatabase.TryGetValue(database.Name, out entityDatabase))
                {
                    return entityDatabase;
                }

                return new EntityDatabase(database);
            }
        }

        /// <summary>
        /// Registers the two document creation functions required to support a custom derived document.
        /// </summary>
        /// <typeparam name="TDocument">The derived document type.</typeparam>
        /// <param name="attachedCreator">
        /// The function that instantiates a derived document from a Couchbase 
        /// Lite <see cref="Document"/>.
        /// </param>
        /// <param name="detachedCreator">
        /// The function that instantiates a derived document from document 
        /// properties, a database reference, and the current revision.
        /// </param>
        /// <param name="attachmentNames">
        /// The case sensitive names of the attachments to be monitored by 
        /// the <see cref="EntityDocument{TEntity}.AttachmentEvent"/>.</param>
        /// <remarks>
        /// This method must be called once for every derived document type before attempting
        /// a database operation using the type.
        /// </remarks>
        public static void Register<TDocument>(
            Func<Document, IEntityDocument> attachedCreator,
            Func<IDictionary<string, object>, EntityDatabase, Revision, IEntityDocument> detachedCreator,
            string[] attachmentNames)

            where TDocument : class, IEntityDocument
        {
            Covenant.Requires<ArgumentNullException>(attachedCreator != null);
            Covenant.Requires<ArgumentNullException>(detachedCreator != null);

            typeToDerivedCreateInfo.TryAdd(typeof(TDocument),
                new DerivedCreateInfo()
                {
                    AttachedCreator = attachedCreator,
                    DetachedCreator = detachedCreator,
                    AttachmentNames = new HashSet<string>(attachmentNames)
                });
        }

        /// <summary>
        /// Returns the derived document creation information for the specified document type.
        /// </summary>
        /// <param name="documentType">The document type.</param>
        /// <returns>The associated <see cref="DerivedCreateInfo"/>.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the requested document type has not been registered.</exception>
        internal static DerivedCreateInfo GetDocumentCreateInfo(Type documentType)
        {
            Covenant.Requires<ArgumentNullException>(documentType != null);
            Covenant.Requires(typeof(IEntityDocument).IsAssignableFrom(documentType), $"Type [{documentType.FullName}] does not implement [{nameof(IEntityDocument)}].");

            DerivedCreateInfo derivedCreateInfo;

            if (!typeToDerivedCreateInfo.TryGetValue(documentType, out derivedCreateInfo))
            {
                throw new KeyNotFoundException($"Document binder type [{documentType.FullName}] has not been registered.");
            }

            return derivedCreateInfo;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="database">The underlying <see cref="Lite.Database"/>.</param>
        /// <exception cref="InvalidOperationException">Thrown if the <paramref name="database"/> has already been associated with another entity database instance.</exception>
        private EntityDatabase(Database database)
        {
            Covenant.Requires<ArgumentNullException>(database != null);

            this.Base = database;

            nameToDatabase.Add(database.Name, this);

            WeakEventController.AddHandler<Database, DatabaseChangeEventArgs>(database, nameof(Base.Changed), OnDatabaseChanged);

            // Initialize the temporary database folder.  This will located in the
            // same location as the Couchbase Lite database folder and will be
            // called [NAME.tmp] where NAME is the Couchbase Lite database name.
            //
            // Note that we're going to purge any existing files in the database
            // to ensure that orphaned attachments don't accumulate.

            // $hack(jeff.lill):
            //
            // This relies on [Database.ToString()] embedding the database folder
            // path like:
            //
            //      "Database[C:\Users\jeff\AppData\Local\Temp\c840daac-9a1a-4b80-90ae-d903e9a0d090\test.cblite2]"
            //
            // and also that the attachment blobs are stored in the [attachments] subdirectory.

            var dbString = database.ToString();
            var posStart = dbString.IndexOf('[') + 1;
            var posEnd   = dbString.LastIndexOfAny(new char[] { '/', '\\' });
            var folder   = dbString.Substring(posStart, posEnd - posStart);

            BlobFolderPath = Path.Combine(dbString.Substring(posStart, dbString.Length - posStart - 1), "attachments");

            if (!Directory.Exists(BlobFolderPath))
            {
                throw new NotImplementedException(NotCompatibleError);
            }

            TempFolderPath = Path.Combine(folder, $"{database.Name}.tmp");

            if (Directory.Exists(TempFolderPath))
            {
                Directory.Delete(TempFolderPath, true);
            }

            Directory.CreateDirectory(TempFolderPath);
        }

        /// <summary>
        /// Returns the underlying <see cref="Lite.Database"/>.
        /// </summary>
        public Database Base { get; private set; }

        /// <summary>
        /// Returns the file system path to the folder where the database persists 
        /// the attachment blob files.
        /// </summary>
        internal string BlobFolderPath { get; private set; }

        /// <summary>
        /// Returns the file system path to the folder where the database will persist temporary
        /// files such as attachment contents before they are persisted to the database.
        /// </summary>
        internal string TempFolderPath { get; private set; }

        /// <summary>
        /// Relays database changed events received from the underlying database.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The event arguments.</param>
        private void OnDatabaseChanged(object sender, DatabaseChangeEventArgs args)
        {
            Changed?.Invoke(this, args);
        }

        /// <summary>
        /// Creates a new <see cref="EntityDocument{TEntity}"/> with a unique ID.
        /// </summary>
        /// <typeparam name="TEntity">The document content type.</typeparam>
        /// <returns>The new document.</returns>
        /// <remarks>
        /// <para>
        /// New documents are empty when initially created and are implicitly read/write 
        /// and have their <see cref="EntityDocument{TEntity}.IsModified"/> property
        /// set to <c>true</c>. 
        /// </para>
        /// </remarks>
        public EntityDocument<TEntity> CreateEntityDocument<TEntity>()
            where TEntity : class, IDynamicEntity, new()
        {
            return new EntityDocument<TEntity>(Stub.Param, Base.CreateDocument());
        }

        /// <summary>
        /// Gets or creates a <see cref="EntityDocument{TEntity}"/> with the specified ID.
        /// </summary>
        /// <typeparam name="TEntity">The document content type.</typeparam>
        /// <param name="id">The document ID.</param>
        /// <returns>The existing or newly created document.</returns>
        public EntityDocument<TEntity> GetEntityDocument<TEntity>(string id)
            where TEntity : class, IDynamicEntity, new()
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(id));

            return new EntityDocument<TEntity>(Stub.Param, Base.GetDocument(id));
        }

        /// <summary>
        /// Gets an existing document as a <see cref="EntityDocument{TEntity}"/> with the specified ID.
        /// </summary>
        /// <typeparam name="TEntity">The document content type.</typeparam>
        /// <param name="id">The document ID.</param>
        /// <returns>The existing document if present or <c>null</c>.</returns>
        public EntityDocument<TEntity> GetExistingEntityDocument<TEntity>(string id)
            where TEntity : class, IDynamicEntity, new()
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(id));

            var document = Base.GetExistingDocument(id);

            if (document == null)
            {
                return null;
            }

            return new EntityDocument<TEntity>(Stub.Param, document);
        }

        /// <summary>
        /// Creates a new binder document with a unique ID.
        /// </summary>
        /// <typeparam name="TDocument">The binder document type.</typeparam>
        /// <returns>The new document.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if <typeparamref name="TDocument"/> was not previously registered by a call to 
        /// <see cref="EntityDatabase.Register(Func{Document, IEntityDocument}, Func{IDictionary{string, object}, EntityDatabase, Revision, IEntityDocument}, string[])"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// New documents are empty when initially created and are implicitly read/write 
        /// and have their <see cref="EntityDocument{TEntity}.IsModified"/> property
        /// set to <c>true</c>. 
        /// </para>
        /// </remarks>
        public TDocument CreateBinderDocument<TDocument>()
            where TDocument : class, IEntityDocument
        {
            return EntityDocument<StubEntity>.Create<TDocument>(Base.CreateDocument());
        }

        /// <summary>
        /// Gets or creates an binder document with the specified ID.
        /// </summary>
        /// <typeparam name="TDocument">The binder document type.</typeparam>
        /// <param name="id">The document ID.</param>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if <typeparamref name="TDocument"/> was not previously registered by a call to 
        /// <see cref="EntityDatabase.Register(Func{Document, IEntityDocument}, Func{IDictionary{string, object}, EntityDatabase, Revision, IEntityDocument}, string[])"/>.
        /// </exception>
        /// <returns>The existing or newly created document.</returns>
        public TDocument GetBinderDocument<TDocument>(string id)
            where TDocument : class, IEntityDocument
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(id));

            return EntityDocument<StubEntity>.Create<TDocument>(Base.GetDocument(id));
        }

        /// <summary>
        /// Gets an existing binder document with the specified ID.
        /// </summary>
        /// <typeparam name="TDocument">The binder document type.</typeparam>
        /// <param name="id">The document ID.</param>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if <typeparamref name="TDocument"/> was not previously registered by a call to 
        /// <see cref="EntityDatabase.Register{TDocument}(Func{Document, IEntityDocument}, Func{IDictionary{string, object}, EntityDatabase, Revision, IEntityDocument}, string[])"/>.
        /// </exception>
        /// <returns>The existing document if present or <c>null</c>.</returns>
        public TDocument GetExistingBinderDocument<TDocument>(string id)
            where TDocument : class, IEntityDocument
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(id));

            var document = Base.GetExistingDocument(id);

            if (document == null)
            {
                return null;
            }

            return EntityDocument<StubEntity>.Create<TDocument>(document);
        }

        //---------------------------------------------------------------------
        // IEntityContext implementation

        /// <inheritdoc/>
        public TEntity LoadEntity<TEntity>(string link, out Func<bool> isDeletedFunc) 
            where TEntity : class, IDynamicEntity, new()
        {
            var document = GetExistingEntityDocument<TEntity>(link);

            if (document != null)
            {
                isDeletedFunc = () => document.IsDeleted;

                return document.Content;
            }

            isDeletedFunc = null;

            return null;
        }

        /// <inheritdoc/>
        public TDocument LoadDocument<TDocument>(string link, out Func<bool> isDeletedFunc)
            where TDocument : class, IDynamicDocument
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(link));

            var baseDocument = Base.GetExistingDocument(link);

            if (baseDocument == null)
            {
                isDeletedFunc = null;
                return null;
            }

            var document = EntityDocument<StubEntity>.Create(typeof(TDocument), baseDocument);

            if (document != null)
            {
                isDeletedFunc = () => document.IsDeleted;

                return (TDocument)document;
            }

            isDeletedFunc = null;
            return null;
        }

        //---------------------------------------------------------------------
        // IDispose implementation

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Base == null)
            {
                return;
            }

            var name = Base.Name;

            WeakEventController.RemoveHandler<Database, DatabaseChangeEventArgs>(Base, nameof(Base.Changed), OnDatabaseChanged);

            Base.Dispose();
            Base = null;

            lock (syncRoot)
            {
                nameToDatabase.Remove(name);
            }
        }

        //---------------------------------------------------------------------
        // Underlying Database wrapper members.

        /// <summary>
        /// Event handler delegate that will be called whenever a <see cref="Couchbase.Lite.Document"/> within the <see cref="Couchbase.Lite.Database"/> changes.
        /// </summary>
        public event EventHandler<DatabaseChangeEventArgs> Changed;

        /// <summary>
        /// Gets or sets an object that can compile source code into <see cref="FilterDelegate"/>.
        /// </summary>
        /// <value>The filter compiler object.</value>
        public static IFilterCompiler FilterCompiler
        {
            get { return Database.FilterCompiler; }
            set { Database.FilterCompiler = value; }
        }

        /// <summary>
        /// Gets all the running <see cref="Couchbase.Lite.Replication" />s 
        /// for this <see cref="Couchbase.Lite.Database" />.  
        /// This includes all continuous <see cref="Couchbase.Lite.Replication" />s and 
        /// any non-continuous <see cref="Couchbase.Lite.Replication" />s that has been started 
        /// and are still running.
        /// </summary>
        /// <value>All replications.</value>
        public IEnumerable<Replication> AllReplications
        {
            get { return Base.AllReplications; }
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Manager" /> that owns this <see cref="Couchbase.Lite.Database"/>.
        /// </summary>
        /// <value>The manager object.</value>
        public Manager Manager
        {
            get { return Base.Manager; }
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Database"/> name.
        /// </summary>
        /// <value>The database name.</value>
        public string Name
        {
            get { return Base.Name; }
        }

        /// <summary>
        /// Change the encryption key used to secure this database
        /// </summary>
        /// <param name="newKey">The new key to use</param>
        public void ChangeEncryptionKey(SymmetricKey newKey)
        {
            Base.ChangeEncryptionKey(newKey);
        }

        /// <summary>
        /// Compacts the <see cref="Couchbase.Lite.Database" /> file by purging non-current 
        /// <see cref="Couchbase.Lite.Revision" />s and deleting unused <see cref="Couchbase.Lite.Attachment" />s.
        /// </summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">thrown if an issue occurs while 
        /// compacting the <see cref="Couchbase.Lite.Database" /></exception>
        public bool Compact()
        {
            return Base.Compact();
        }

        /// <summary>
        /// Creates a <see cref="Couchbase.Lite.Query" /> that matches all <see cref="Couchbase.Lite.Document" />s in the <see cref="Couchbase.Lite.Database" />.
        /// </summary>
        /// <returns>Returns a <see cref="Couchbase.Lite.Query" /> that matches all <see cref="Couchbase.Lite.Document" />s in the <see cref="Couchbase.Lite.Database" />s.</returns>
        public Query CreateAllDocumentsQuery()
        {
            return Base.CreateAllDocumentsQuery();
        }

        /// <summary>
        /// Creates a <see cref="Couchbase.Lite.Document" /> with a unique id.
        /// </summary>
        /// <returns>A document with a unique id.</returns>
        public Document CreateDocument()
        {
            return Base.CreateDocument();
        }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.Replication"/> that will pull from the source <see cref="Couchbase.Lite.Database"/> at the given url.
        /// </summary>
        /// <returns>A new <see cref="Couchbase.Lite.Replication"/> that will pull from the source Database at the given url.</returns>
        /// <param name="url">The url of the source Database.</param>
        public Replication CreatePullReplication(Uri url)
        {
            return Base.CreatePullReplication(url);
        }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.Replication"/> that will push to the target <see cref="Couchbase.Lite.Database"/> at the given url.
        /// </summary>
        /// <returns>A new <see cref="Couchbase.Lite.Replication"/> that will push to the target <see cref="Couchbase.Lite.Database"/> at the given url.</returns>
        /// <param name="url">The url of the target Database.</param>
        public Replication CreatePushReplication(Uri url)
        {
            return Base.CreatePushReplication(url);
        }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.Database" />.
        /// </summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while deleting the <see cref="Couchbase.Lite.Database" /></exception>
        public void Delete()
        {
            Base.Delete();
        }

        /// <summary>
        /// Deletes the local <see cref="Couchbase.Lite.Document" /> with the given id.
        /// </summary>
        /// <returns><c>true</c>, if local <see cref="Couchbase.Lite.Document" /> was deleted, <c>false</c> otherwise.</returns>
        /// <param name="id">Identifier.</param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">Thrown if there is an issue occurs while deleting the local document.</exception>
        public bool DeleteLocalDocument(string id)
        {
            return Base.DeleteLocalDocument(id);
        }

        /// <summary>
        /// Gets or creates the <see cref="Couchbase.Lite.Document" /> with the given id.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.Document" />.</returns>
        /// <param name="id">The id of the Document to get or create.</param>
        public Document GetDocument(string id)
        {
            return Base.GetDocument(id);
        }

        /// <summary>
        /// Gets the number of <see cref="Couchbase.Lite.Document" /> in the <see cref="Couchbase.Lite.Database"/>.
        /// </summary>
        /// <returns>The document count.</returns>
        public int GetDocumentCount()
        {
            return Base.GetDocumentCount();
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.Document" /> with the given id, or null if it does not exist.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.Document" /> with the given id, or null if it does not exist.</returns>
        /// <param name="id">The id of the Document to get.</param>
        public Document GetExistingDocument(string id)
        {
            return Base.GetExistingDocument(id);
        }

        /// <summary>
        /// Gets the local document with the given id, or null if it does not exist.
        /// </summary>
        /// <returns>The existing local document.</returns>
        /// <param name="id">Identifier.</param>
        public IDictionary<string, object> GetExistingLocalDocument(string id)
        {
            return Base.GetExistingLocalDocument(id);
        }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.View" /> with the given name, or null if it does not exist.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.View" /> with the given name, or null if it does not exist.</returns>
        /// <param name="name">The name of the View to get.</param>
        public View GetExistingView(string name)
        {
            return Base.GetExistingView(name);
        }

        /// <summary>
        /// Returns the <see cref="ValidateDelegate" /> for the given name, or null if it does not exist.
        /// </summary>
        /// <returns>The <see cref="ValidateDelegate" /> for the given name, or null if it does not exist.</returns>
        /// <param name="name">The name of the validation delegate to get.</param>
        /// <param name="status">The result of the operation</param>
        public FilterDelegate GetFilter(string name, Status status = null)
        {
            return Base.GetFilter(name, status);
        }

        /// <summary>
        /// Gets the latest sequence number used by the <see cref="Couchbase.Lite.Database" />.  Every new <see cref="Couchbase.Lite.Revision" /> is assigned a new sequence 
        /// number, so this property increases monotonically as changes are made to the <see cref="Couchbase.Lite.Database" />. This can be used to 
        /// check whether the <see cref="Couchbase.Lite.Database" /> has changed between two points in time.
        /// </summary>
        /// <returns>The last sequence number.</returns>
        public long GetLastSequenceNumber()
        {
            return Base.GetLastSequenceNumber();
        }

        /// <summary>
        /// Maximum depth of a document's revision tree (or, max length of its revision history.)
        /// Revisions older than this limit will be deleted during a -compact: operation.
        /// </summary>
        /// <remarks>
        /// Maximum depth of a document's revision tree (or, max length of its revision history.)
        /// Revisions older than this limit will be deleted during a -compact: operation.
        /// Smaller values save space, at the expense of making document conflicts somewhat more likely.
        /// </remarks>
        /// <returns>The maximum depth set on this Database</returns>
        public int GetMaxRevTreeDepth()
        {
            return Base.GetMaxRevTreeDepth();
        }

        /// <summary>
        /// Gets the total size of the database on the filesystem.
        /// </summary>
        /// <returns>The total size of the database on the filesystem.</returns>
        public long GetTotalDataSize()
        {
            return Base.GetTotalDataSize();
        }

        /// <summary>
        /// Gets the <see cref="ValidateDelegate" /> for the given name, or null if it does not exist.
        /// </summary>
        /// <returns>the <see cref="ValidateDelegate" /> for the given name, or null if it does not exist.</returns>
        /// <param name="name">The name of the validation delegate to get.</param>
        public ValidateDelegate GetValidation(string name)
        {
            return Base.GetValidation(name);
        }

        /// <summary>
        /// Gets or creates the <see cref="Couchbase.Lite.View" /> with the given name.  
        /// New <see cref="Couchbase.Lite.View" />s won't be added to the <see cref="Couchbase.Lite.Database" /> 
        /// until a map function is assigned.
        /// </summary>
        /// <returns>The <see cref="Couchbase.Lite.View" /> with the given name.</returns>
        /// <param name="name">The name of the <see cref="Couchbase.Lite.View" /> to get or create.</param>
        public View GetView(string name)
        {
            return Base.GetView(name);
        }

        /// <summary>
        /// Sets the contents of the local <see cref="Couchbase.Lite.Document" /> with the given id.  If
        /// <paramref name="properties"/> is null, the <see cref="Couchbase.Lite.Document" /> is deleted.
        /// </summary>
        /// <param name="id">The id of the local document whos contents to set.</param>
        /// <param name="properties">The contents to set for the local document.</param>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException">
        /// Thrown if an issue occurs while setting the contents of the local document.
        /// </exception>
        public bool PutLocalDocument(string id, IDictionary<string, object> properties)
        {
            return Base.PutLocalDocument(id, properties);
        }

        /// <summary>
        /// Runs the <see cref="Couchbase.Lite.RunAsyncDelegate"/> asynchronously.
        /// </summary>
        /// <returns>The async task.</returns>
        /// <param name="runAsyncDelegate">The delegate to run asynchronously.</param>
        public Task RunAsync(RunAsyncDelegate runAsyncDelegate)
        {
            return Base.RunAsync(runAsyncDelegate);
        }

        /// <summary>
        /// Runs the delegate within a transaction. If the delegate returns false, 
        /// the transaction is rolled back.
        /// </summary>
        /// <returns>True if the transaction was committed, otherwise false.</returns>
        /// <param name="transactionDelegate">The delegate to run within a transaction.</param>
        public bool RunInTransaction(RunInTransactionDelegate transactionDelegate)
        {
            return Base.RunInTransaction(transactionDelegate);
        }

        /// <summary>
        /// Sets the <see cref="ValidateDelegate" /> for the given name. If delegate is null, the filter 
        /// with the given name is deleted. Before a <see cref="Couchbase.Lite.Revision" /> is replicated via a 
        /// push <see cref="Couchbase.Lite.Replication" />, its filter delegate is called and 
        /// given a chance to exclude it from the <see cref="Couchbase.Lite.Replication" />.
        /// </summary>
        /// <param name="name">The name of the filter delegate to set.</param>
        /// <param name="filterDelegate">The filter delegate to set.</param>
        public void SetFilter(string name, FilterDelegate filterDelegate)
        {
            SetFilter(name, filterDelegate);
        }

        /// <summary>
        /// Sets the maximum depth of a document's revision tree (or, max length of its revision history.)
        /// Revisions older than this limit will be deleted during a -compact: operation.
        /// </summary>
        /// <remarks>
        /// Maximum depth of a document's revision tree (or, max length of its revision history.)
        /// Revisions older than this limit will be deleted during a -compact: operation.
        /// Smaller values save space, at the expense of making document conflicts somewhat more likely.
        /// </remarks>
        /// <param name="value">The new maximum depth to use for this Database</param> 
        public void SetMaxRevTreeDepth(int value)
        {
            Base.SetMaxRevTreeDepth(value);
        }

        /// <summary>
        /// Sets the validation delegate for the given name. If delegate is null, 
        /// the validation with the given name is deleted. Before any change 
        /// to the <see cref="Couchbase.Lite.Database"/> is committed, including incoming changes from a pull 
        /// <see cref="Couchbase.Lite.Replication"/>, all of its validation delegates are called and given 
        /// a chance to reject it.
        /// </summary>
        /// <param name="name">The name of the validation delegate to set.</param>
        /// <param name="validationDelegate">The validation delegate to set.</param>
        public void SetValidation(string name, ValidateDelegate validationDelegate)
        {
            Base.SetValidation(name, validationDelegate);
        }

        /// <Inheritdoc/>
        public override string ToString()
        {
            return Base.ToString();
        }
    }
}
