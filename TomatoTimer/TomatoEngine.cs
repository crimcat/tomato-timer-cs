using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TomatoTimer {

    /// <summary>
    /// Minutes counter with callbacks: callback on each minute and callback on all time period finished
    /// </summary>
    class MinutesTimer {

        public delegate void OneMinutePassed();
        public delegate void TimeCounterFinished();

        /// <summary>
        /// Create minutes counter with two callback. Callbacks are mandatory, cannot be null.
        /// </summary>
        /// <param name="minutesNotifier"></param>
        /// <param name="timeFinishedNotifier"></param>
        internal MinutesTimer(OneMinutePassed minutesNotifier, TimeCounterFinished timeFinishedNotifier) {
            if((null == minutesNotifier) || (null == timeFinishedNotifier)) {
                throw new ArgumentNullException();
            }
            this.minutesNotifier = minutesNotifier;
            this.timeFinishedNotifier = timeFinishedNotifier;
        }

        /// <summary>
        /// Start counter for the given number of minutes.
        /// </summary>
        /// <param name="minutes"></param>
        public void Go(uint minutes) {
            if(MinutesLeft == 0) {
                MinutesLeft = minutes;
                worker = new Thread(() => {
                    int gransCounter = 0;
                    while(Thread.CurrentThread.IsAlive) {
                        Thread.Sleep(GRANULARITY_IN_SECONDS * 1000);
                        if(!isPaused) {
                            gransCounter += GRANULARITY_IN_SECONDS;
                            if(gransCounter >= ONE_MINUTE) {
                                gransCounter = 0;
                                minutesNotifier();
                                if(DecrementMinutesCounter()) {
                                    timeFinishedNotifier();
                                    break;
                                }
                            }
                        }
                    }
                });
                worker.Start();
            }
        }

        /// <summary>
        /// Pause minutes counter. If it wasn't started before the method does nothing.
        /// </summary>
        public void Pause() {
            if(MinutesLeft > 0) {
                isPaused = true;
            }
        }

        /// <summary>
        /// Start minutes counter agian. If it wasn't started and paused before the method does nothing.
        /// </summary>
        public void Proceed() {
            if(isPaused && (MinutesLeft > 0)) {
                isPaused = false;
            }
        }

        /// <summary>
        /// Cancel currently running (or being paused) minutes counter.
        /// </summary>
        public void Cancel() {
            if((null != worker) && worker.IsAlive) {
                isPaused = false;
                worker.Abort();
            }
            worker = null;
        }

        /// <summary>
        /// Property: how many minutes are still left to run.
        /// </summary>
        public uint MinutesLeft { get; private set; } = 0;

        /// <summary>
        /// Decrement number of minutes left.
        /// </summary>
        /// <returns>Return true if counter finished</returns>
        private bool DecrementMinutesCounter() {
            if(MinutesLeft > 0) {
                MinutesLeft -= 1;
            }
            return (0 == MinutesLeft);
        }

        private const int GRANULARITY_IN_SECONDS = 2;
        private const int ONE_MINUTE = 60;
        private bool isPaused = false;

        private Thread worker = null;

        private readonly OneMinutePassed minutesNotifier;
        private readonly TimeCounterFinished timeFinishedNotifier;
    }

    public class TomatoEngine {

        /// <summary>
        /// Engine state changed callback.
        /// </summary>
        public delegate void EngineStateChanged();
        /// <summary>
        /// Time interval (1 minute) is passed callback
        /// </summary>
        public delegate void ControlledIntervalElapsed();

        /// <summary>
        /// Possible engine states
        /// </summary>
        public enum State {
            /// <summary>
            /// Engine is not running.
            /// </summary>
            IDLE,
            /// <summary>
            /// Engine is running and we're in work time interval
            /// </summary>
            WORKING,
            /// <summary>
            /// Engine is running and we're in break time interval
            /// </summary>
            BREAK,
            /// <summary>
            /// Engine is not running, time counter has been finished
            /// </summary>
            FINISHED,
            /// <summary>
            /// Engine is running, we're in work time interval and time counter is paused
            /// </summary>
            PAUSED,
            /// <summary>
            /// Engine is running, we're in break time interval and time counter is paused
            /// </summary>
            PAUSED_IN_BREAK
        }

        /// <summary>
        /// Create engine object with given parameters.
        /// </summary>
        /// <param name="bunchSize">Number of tomatos in the bunch</param>
        /// <param name="tomatoDuration">Work time period (tomato) duration in minutes</param>
        /// <param name="breakDuration">Break time period duration in minutes</param>
        public TomatoEngine(uint bunchSize, uint tomatoDuration, uint breakDuration) {
            this.bunchSize = bunchSize;
            this.tomatoDuration = tomatoDuration;
            this.breakDuration = breakDuration;

            this.minutesTimer = new MinutesTimer(
                () => { controlledIntervalElapsed(); },
                () => {
                    CheckUpdateBunchCounter();
                    currentState = PeekNextState();
                    if(currentState != State.FINISHED) {
                        minutesTimer.Go(CurrentStateDuration);
                    }
                    engineStateChanged();
                }
            );
        }

        /// <summary>
        /// Set callback for engine start change
        /// </summary>
        /// <param name="engineStateChanged">Engine state change delegate</param>
        /// <returns></returns>
        public TomatoEngine SetEngineStateChangedCallback(EngineStateChanged engineStateChanged) {
            this.engineStateChanged = engineStateChanged;
            return this;
        }

        /// <summary>
        /// Set callback for engine controlled inverval time passed (1 minutes)
        /// </summary>
        /// <param name="controlledIntervalElapsed">engine controlled interval delegate</param>
        /// <returns></returns>
        public TomatoEngine SetControlledIntervalElapsedCallback(ControlledIntervalElapsed controlledIntervalElapsed) {
            this.controlledIntervalElapsed = controlledIntervalElapsed;
            return this;
        }

        /// <summary>
        /// Get remaining tomatoes (current bunch size), in other words - number of tomatoes left
        /// </summary>
        public uint BunchSize {
            get { return bunchSize; }
        }
        /// <summary>
        /// Get work time (tomato) duration in minutes
        /// </summary>
        public uint TomatoDuration {
            get { return tomatoDuration; }
        }
        /// <summary>
        /// Get break time duration in minutes
        /// </summary>
        public uint BreakDuration {
            get { return breakDuration; }
        }
        /// <summary>
        /// Get duration for current state, returns null for states which do not have duration defined
        /// </summary>
        public uint CurrentStateDuration {
            get {
                switch(currentState) {
                case State.WORKING: return TomatoDuration;
                case State.BREAK: return BreakDuration;
                }
                return 0;
            }
        }
        /// <summary>
        /// Get number of minutes left for the timer (current state)
        /// </summary>
        public uint MinutesToGo {
            get { return minutesTimer.MinutesLeft; }
        }

        /// <summary>
        /// Get engine current state.
        /// <see cref="State"/>
        /// </summary>
        public State CurrentState { get { return currentState; } }

        /// <summary>
        /// Start engine
        /// </summary>
        public void Start() {
            if(State.IDLE == currentState) {
                currentState = State.WORKING;
                minutesTimer.Go(CurrentStateDuration);
            }
        }

        /// <summary>
        /// Pause engine
        /// </summary>
        public void Pause() {
            if(State.WORKING == currentState) {
                currentState = State.PAUSED;
                minutesTimer.Pause();
            }
        }

        /// <summary>
        /// Start paused engine again
        /// </summary>
        public void Proceed() {
            if(State.PAUSED == currentState) {
                currentState = State.WORKING;
                minutesTimer.Proceed();
            }
        }

        /// <summary>
        /// Cancel current engine. Time counter is dropped.
        /// </summary>
        public void Cancel() {
            if(State.IDLE != currentState) {
                currentState = State.FINISHED;
                minutesTimer.Cancel();
            }
        }

        /// <summary>
        /// Decrement bunch counter if it is possible
        /// </summary>
        private void CheckUpdateBunchCounter() {
            if(State.BREAK == currentState) {
                --bunchSize;
            }
        }

        /// <summary>
        /// Calculate next engine state
        /// </summary>
        /// <returns>New state the engine can switch to</returns>
        private State PeekNextState() {
            switch(currentState) {
            case State.IDLE: return State.IDLE;
            case State.WORKING: return BunchSize > 1 ? State.BREAK : State.FINISHED;
            case State.BREAK: return State.WORKING;
            case State.FINISHED: return State.FINISHED;
            case State.PAUSED: return State.WORKING;
            case State.PAUSED_IN_BREAK: return State.BREAK;
            }
            return State.IDLE;
        }
        private State currentState = State.IDLE;

        private uint bunchSize;
        private readonly uint tomatoDuration;
        private readonly uint breakDuration;

        private readonly MinutesTimer minutesTimer;

        private EngineStateChanged engineStateChanged;
        private ControlledIntervalElapsed controlledIntervalElapsed;
    }
}
