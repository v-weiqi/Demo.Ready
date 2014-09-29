using System.IO;

namespace XmlGenCore
{
    public class ClosableStreamWriter : StreamWriter
    {
        public bool CloseStreamOnDispose { get; set; }

        public ClosableStreamWriter(Stream stream)
            : base(stream)
        {
            this.CloseStreamOnDispose = false;
        }

        protected override void Dispose(bool disposeManaged)
        {
            this.Flush();
            base.Dispose(this.CloseStreamOnDispose);
        }
    }
}
