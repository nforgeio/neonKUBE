using System.IO;

namespace Microsoft.Net.Http.Client
{
    /// <summary>
    /// Define a stream interface that implements <see cref="CanCloseWrite"/> and <see cref="CloseWrite"/>.
    /// </summary>
    public abstract class WriteClosableStream : Stream
    {
        /// <summary>
        /// Returns <c>true</c> if the stream can close write operation.
        /// </summary>
        public abstract bool CanCloseWrite { get; }

        /// <summary>
        /// Close write operations.
        /// </summary>
        public abstract void CloseWrite();
    }
}