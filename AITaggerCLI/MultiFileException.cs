namespace AITaggerCLI;

public class MultiFileException : Exception
{
    public string Filename;
    public MultiFileException(string filename,Exception inner) : base ("File Exception.", inner)
    {
        Filename = filename;
    }
}