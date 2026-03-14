namespace AITaggerCLI;

public class MultiFileException : Exception
{
    public string Filename;
    public Exception InnerException;
    public MultiFileException(Exception inner, string filename)
    {
        Filename = filename;
        InnerException = inner;
    }
}