using System;
using System.Collections.Concurrent;
using System.Numerics;

namespace AsyncCombinator.Queue
{
    public class ExecutorQueue<T>
    {
        private readonly ConcurrentQueue<ExecutorQueueItem> _backlogQueue;
        private readonly ConcurrentDictionary<BigInteger, ExecutorQueueItem> _progressQueue;

        private BigInteger _uniqueProgressIdBase;
        public event EventHandler<ExecutorQueueItem> ShouldExecute;
        public int MaximumParallelExecutions { get; set; }

        public ExecutorQueue(int maximumParallelExecutions = 4)
        {
            _backlogQueue = new ConcurrentQueue<ExecutorQueueItem>();
            _progressQueue = new ConcurrentDictionary<BigInteger, ExecutorQueueItem>();
            _uniqueProgressIdBase = new BigInteger(0);
            MaximumParallelExecutions = maximumParallelExecutions;
        }

        private void UpdateProgressQueue()
        {
            while (_progressQueue.Count < MaximumParallelExecutions && !_backlogQueue.IsEmpty)
            {
                var res = _backlogQueue.TryDequeue(out var queueItem);
                if (res)
                {
                    _progressQueue[queueItem.QueueId] = queueItem;
                    ShouldExecute?.Invoke(this, queueItem);
                }
            }
        }

        public void Enqueue(T item)
        {
            _backlogQueue.Enqueue(new ExecutorQueueItem(this, _uniqueProgressIdBase++, item));
            UpdateProgressQueue();
        }

        public void MarkComplete(BigInteger queueId)
        {
            if (!_progressQueue.TryRemove(queueId, out _))
            {
                throw new Exception("Could not remove from queue");
            }
            UpdateProgressQueue();
        }

        public void ClearBacklog()
        {
            while (_backlogQueue.TryDequeue(out _))
            {
            }
        }

        public class ExecutorQueueItem
        {
            public BigInteger QueueId { get; }
            public T Item { get; }
            public ExecutorQueue<T> Owner { get; }

            public ExecutorQueueItem(ExecutorQueue<T> owner, BigInteger queueId, T item)
            {
                Owner = owner;
                QueueId = queueId;
                Item = item;
            }

            public void MarkComplete()
            {
                Owner.MarkComplete(QueueId);
            }
        }
    }
}
