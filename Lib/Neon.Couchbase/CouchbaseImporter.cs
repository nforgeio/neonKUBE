//-----------------------------------------------------------------------------
// FILE:	    CouchbaseImporter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Text;

using Couchbase.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Data
{
    /// <summary>
    /// Handles persisting JSON documents to Couchbase.
    /// </summary>
    public class CouchbaseImporter
    {
        private Action<string>  errorSink;
        private bool            errorsDetected;
        private IBucket         bucket;
        private string          keyPattern;
        private bool            dryRun;
        private long            docNumber;
        private StringBuilder   sbKey;      // Using a common instance for performance

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="errorSink">Action invoked when an error is encountered.</param>
        /// <param name="bucket">The target Couchbase bucket.</param>
        /// <param name="keyPattern">The key pattern (or <c>null</c>).</param>
        /// <param name="firstKey">Optionally specifies the first #MONO_INCR# key (defaults to <b>1</b>).</param>
        /// <param name="dryRun">
        /// Optionally specify that the class should go through the motions but 
        /// not actually persist anything.
        /// </param>
        public CouchbaseImporter(Action<string> errorSink, IBucket bucket, string keyPattern, long firstKey = 1, bool dryRun = false)
        {
            this.errorSink  = errorSink;
            this.bucket     = bucket;
            this.keyPattern = keyPattern;
            this.dryRun     = dryRun;
            this.docNumber  = firstKey;
            this.sbKey      = new StringBuilder();
        }

        /// <summary>
        /// Reports an error.
        /// </summary>
        /// <param name="message">The error message.</param>
        private void ReportError(string message)
        {
            errorSink?.Invoke(message);
            errorsDetected = true;
        }

        /// <summary>
        /// Persists a document to Couchbase.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <remarks>
        /// <note>
        /// This method will remove any <b>@@key</b> property from 
        /// the document if present.
        /// </note>
        /// </remarks>
        public void WriteDocument(JObject document)
        {
            string key;

            if (errorsDetected)
            {
                // Stop writing documents after we've reported errors.

                return;
            }

            if (!string.IsNullOrEmpty(keyPattern))
            {
                // We need to parse the key pattern.

                switch (keyPattern)
                {
                    // Optimize some common cases.

                    case "#UUID#":

                        key = EntityHelper.CreateUuid();
                        break;

                    case "#MONO_INCR#":

                        key = docNumber.ToString();
                        break;

                    // Actually parse the thing. 

                    default:

                        sbKey.Clear();

                        var pos = 0;

                        while (pos < keyPattern.Length)
                        {
                            var ch = keyPattern[pos];

                            switch (ch)
                            {
                                case '#':

                                    if (pos < keyPattern.Length - 1 && keyPattern[pos + 1] == '#')
                                    {
                                        // Escaped '#'

                                        sbKey.Append(ch);
                                        pos++;
                                        break;
                                    }
                                    else
                                    {
                                        // Scan forward for the closing '#'.

                                        var posEnd = keyPattern.IndexOf('#', pos + 1);

                                        if (posEnd == -1)
                                        {
                                            ReportError($"Key pattern [{keyPattern}] is missing the closing [#].");
                                            return;
                                        }

                                        var identifier = keyPattern.Substring(pos + 1, posEnd - (pos + 1));

                                        switch (identifier)
                                        {
                                            case "UUID":

                                                sbKey.Append(EntityHelper.CreateUuid());
                                                break;

                                            case "MONO_INCR":

                                                sbKey.Append(docNumber);
                                                break;

                                            default:

                                                ReportError($"Key pattern [{keyPattern}] includes unknown key generator [#{identifier}#].");
                                                return;
                                        }

                                        pos = posEnd;
                                    }
                                    break;

                                case '%':

                                    if (pos < keyPattern.Length - 1 && keyPattern[pos + 1] == '%')
                                    {
                                        // Escaped '%'

                                        sbKey.Append(ch);
                                        pos++;
                                        break;
                                    }
                                    else
                                    {
                                        // Scan forward for the closing '%'.

                                        var posEnd = keyPattern.IndexOf('%', pos + 1);

                                        if (posEnd == -1)
                                        {
                                            ReportError($"Key pattern [{keyPattern}] is missing the closing [%].");
                                            return;
                                        }

                                        var propertyName  = keyPattern.Substring(pos + 1, posEnd - (pos + 1));
                                        var propertyValue = document.GetValue(propertyName);

                                        if (propertyValue != null)
                                        {
                                            switch (propertyValue.Type)
                                            {
                                                case JTokenType.String:
                                                case JTokenType.Integer:
                                                case JTokenType.Float:
                                                case JTokenType.Guid:
                                                case JTokenType.Date:
                                                case JTokenType.Uri:

                                                    sbKey.Append(propertyValue);
                                                    break;

                                                default:

                                                    ReportError($"Document number [{docNumber}] has an invalid [{propertyName}] property referenced by the key pattern.");
                                                    return;
                                            }

                                            pos = posEnd;
                                        }
                                        else
                                        {
                                            ReportError($"Document number [{docNumber}] is missing the [{propertyName}] property referenced by the key pattern.");
                                            return;
                                        }
                                    }
                                    break;

                                default:

                                    sbKey.Append(ch);
                                    break;
                            }

                            pos++;
                        }

                        key = sbKey.ToString();
                        break;
                }
            }
            else
            {
                // We don't have a key pattern so we'll look for a
                // [@@key] property and use that value if present.
                // Otherwise, we'll generate a UUID.

                var keyProperty = document.GetValue("@@key");

                if (keyProperty != null)
                {
                    switch (keyProperty.Type)
                    {
                        case JTokenType.String:
                        case JTokenType.Integer:
                        case JTokenType.Float:
                        case JTokenType.Guid:
                        case JTokenType.Date:
                        case JTokenType.Uri:

                            key = keyProperty.ToString();
                            break;

                        default:

                            key = EntityHelper.CreateUuid();
                            break;
                    }
                }
                else
                {
                    key = EntityHelper.CreateUuid();
                }

                document.Remove("@@key"); // We don't persist the special key property.
            }

            if (!dryRun)
            {
                bucket.Upsert(key, document);
            }

            docNumber++;
        }

        /// <summary>
        /// Returns the number of persisted documents.
        /// </summary>
        public long DocumentCount
        {
            get { return docNumber - 1; }
        }
    }
}
