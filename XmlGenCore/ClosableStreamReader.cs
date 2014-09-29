using System.IO;

namespace XmlGenCore
{
    public class ClosableStreamReader : StreamReader
    {
        public bool CloseStreamOnDispose { get; set; }

        public ClosableStreamReader(Stream stream)
            : base(stream)
        {
            this.CloseStreamOnDispose = false;
        }

        protected override void Dispose(bool disposeManaged)
        {
            base.Dispose(this.CloseStreamOnDispose);
        }
    }
}