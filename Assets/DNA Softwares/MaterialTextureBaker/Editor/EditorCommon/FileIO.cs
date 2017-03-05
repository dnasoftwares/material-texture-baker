using System.IO;

namespace DNASoftwares.Unity
{
    public static class FileIO
    {
        public static void WriteAllBytes(string path, byte[] bytes)
        {
            var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            
            BinaryWriter bw =new BinaryWriter(fs);
            bw.Write(bytes);
            bw.Close();
            fs.Close();
        }
    }
}