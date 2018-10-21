using System.Threading.Tasks;
using Hangman.Instance.NamedPipe.Utilities;

namespace Hangman.Instance.NamedPipe.Interfaces
{
    public interface ICommunicationClient : ICommunication
    {
        /// <summary>
        /// This method sends the given message asynchronously over the communication channel
        /// </summary>
        /// <param name="message"></param>
        /// <returns>A task of TaskResult</returns>
        Task<TaskResult> SendMessage(string message);
    }
}
