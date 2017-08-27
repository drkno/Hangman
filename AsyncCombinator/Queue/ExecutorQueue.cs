using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

namespace AsyncCombinator.Queue
{
    public class ExecutorQueue<T> : IEnumerable<ExecutorQueue<T>.ExecutorQueueItem>
    {
        private readonly ConcurrentQueue<ExecutorQueueItem> _backlogQueue;
        private readonly ConcurrentDictionary<BigInteger, ExecutorQueueItem> _progressQueue;

        private BigInteger _uniqueProgressIdBase;
        private int _maximumParallelExecutions;
        public event EventHandler<ExecutorQueueItem> ShouldExecute;

        public int MaximumParallelExecutions
        {
            get => _maximumParallelExecutions;
            set
            {
                _maximumParallelExecutions = value;
                UpdateProgressQueue();
            }
        }

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

        public ExecutorQueueItem MarkComplete(BigInteger queueId)
        {
            if (!_progressQueue.TryRemove(queueId, out var item))
            {
                throw new Exception("Could not remove from queue");
            }
            UpdateProgressQueue();
            return item;
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

        public class ExecutorQueueEnumerator : IEnumerator<ExecutorQueueItem>
        {
            private IEnumerator<KeyValuePair<BigInteger, ExecutorQueueItem>> _progress;
            private IEnumerator<ExecutorQueueItem> _backlog;
            private bool _curr;
            private readonly ExecutorQueue<T> _queue;

            public ExecutorQueueEnumerator(ExecutorQueue<T> queue)
            {
                _curr = true;
                _queue = queue;
                _progress = queue._progressQueue.GetEnumerator();
                _backlog = queue._backlogQueue.GetEnumerator();
            }

            public void Dispose()
            {
                _progress.Dispose();
                _backlog.Dispose();
            }

            public bool MoveNext()
            {
                if (_curr && _progress.MoveNext())
                    return true;
                if (_curr)
                    _curr = false;
                return _backlog.MoveNext();
            }

            public void Reset()
            {
                _curr = true;
                try
                {
                    _progress.Reset();
                }
                catch
                {
                    _progress = _queue._progressQueue.GetEnumerator();
                }
                try
                {
                    _backlog.Reset();
                }
                catch
                {
                    _backlog = _queue._backlogQueue.GetEnumerator();
                }
            }

            public ExecutorQueueItem Current => _curr ? _progress.Current.Value : _backlog.Current;

            object IEnumerator.Current => Current;
        }

        public IEnumerator<ExecutorQueueItem> GetEnumerator()
        {
            return new ExecutorQueueEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
