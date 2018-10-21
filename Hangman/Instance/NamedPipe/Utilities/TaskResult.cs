namespace Hangman.Instance.NamedPipe.Utilities
{
    public class TaskResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class TaskResult<T>
    {
        public T Result { get; set; }
    }
}
