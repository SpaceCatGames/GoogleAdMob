using System;
using System.Collections.Generic;
using System.Threading;
using SCG.Core.Components;
using UnityEngine;

namespace SpaceCatGames.OpenSource
{
    /// <summary>
    /// A thread-safe class which holds a queue with actions to execute on the next Update() method.
    /// It can be used to make calls to the main thread for things such as UI Manipulation in Unity etc
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour /*Singleton<MainThreadDispatcher>*/
    {
        public static MainThreadDispatcher Instance { get; private set; }

        public Thread MainUnityThread { get; private set; }

        private readonly Queue<Action> taskQueue = new Queue<Action>();

        protected void Awake()
        {
            Instance = this;
            MainUnityThread = Thread.CurrentThread;
        }

        protected void Update()
        {
            if ( !Application.isPlaying )
                return;

            if ( Thread.CurrentThread == MainUnityThread )
                 DispatchTasks();
        }

        /// <summary>
        /// Locks the queue and adds the Action to the queue
        /// </summary>
        /// <param name="action">Function that will be executed from the main thread.</param>
        public void Enqueue( Action action )
        {
            if ( Thread.CurrentThread == MainUnityThread )
            {
                action();
                return;
            }

            lock ( taskQueue )
            {
                taskQueue.Enqueue( action );
            }
        }

        private void DispatchTasks()
        {
            lock ( taskQueue )
            {
                if ( taskQueue.Count == 0 )
                    return;

                try
                {
                    taskQueue.Dequeue()?.Invoke();
                }
                catch ( Exception ex )
                {
                    Debug.LogError( ex );
                }
            }
        }
    }
}