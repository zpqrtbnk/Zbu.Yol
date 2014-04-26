using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Logging;

namespace Zbu.Yol
{
    // initial state (ie if there is no state file) is string.Empty, so to begin with there
    // should be a transition from string.Empty to another state.
    // states should be unique strings with a minimum risk of collision, so guids are a safe
    // choice. a state cannot be null nor empty.
    // it is possible to remove old transition definitions - the risk being that an Umbraco
    // install that would be in an older state would crash.

    /// <summary>
    /// Represents the state machine.
    /// </summary>
    public class YolManager
    {
        private readonly string _statePath;
        private readonly string _login;
        private readonly Dictionary<string, YolTransition> _transitions
            = new Dictionary<string, YolTransition>(StringComparer.InvariantCultureIgnoreCase);

        private static YolManager _current;

        // internal for tests
        internal YolManager()
        {
            _statePath = null;
            State = string.Empty;
        }

        // the following one is probably never going to be used publically?
        // keep it internal for the time being.
        
        /// <summary>
        /// Initializes a new instance of the <see cref="YolManager"/> class with a state file path and a user login.
        /// </summary>
        /// <param name="statePath">The full path of the file containing the state.</param>
        /// <param name="login">The login of the user to impersonate while running the state machine.</param>
        /// <exception cref="ArgumentException"><paramref name="statePath"/> and/or <paramref name="login"/> is <c>null</c> or empty.</exception>
        internal YolManager(string statePath, string login)
        {
            if (string.IsNullOrWhiteSpace(statePath))
                throw new ArgumentException("Cannot be null nor empty.", "statePath");
            if (string.IsNullOrWhiteSpace(login))
                throw new ArgumentException("Cannot be null nor empty.", "login");

            _statePath = statePath;
            _login = login;
        }

        // for tests
        internal string State { get; private set; }

        private string GetState()
        {
            try
            {
                if (_statePath == null) return State;
                return State = (File.Exists(_statePath) ? File.ReadAllText(_statePath) : string.Empty);
            }
            catch (Exception e)
            {
                throw new Exception("Could not read state because reading the state file failed.", e);
            }
        }

        private void SaveState(string origState, string state)
        {
            var currentState = GetState();
            if (currentState != origState)
                throw new Exception("Could not save state because the state in the file has changed.");
            try
            {
                if (_statePath != null)
                    File.WriteAllText(_statePath, state);
                State = state;
            }
            catch (Exception e)
            {                
                throw new Exception("Could not save state because writing to the state file failed.", e);
            }
        }

        /// <summary>
        /// Defines a transition.
        /// </summary>
        /// <param name="sourceState">The source state of the transition.</param>
        /// <param name="targetState">The target state of the transition.</param>
        /// <param name="transition">The function to execute with the transition.</param>
        /// <returns>This instance of the <see cref="YolManager"/> class.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="sourceState"/> and/or <paramref name="transition"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="targetState"/> is <c>null</c> or empty, or equal to <paramref name="sourceState"/>.</exception>
        /// <exception cref="InvalidOperationException">A transition from the specified source state already exists.</exception>
        public YolManager DefineTransition(string sourceState, string targetState, Func<ApplicationContext, HttpServerUtility, bool> transition)
        {
            if (sourceState == null)
                throw new ArgumentNullException("sourceState");
            if (string.IsNullOrWhiteSpace(targetState))
                throw new ArgumentException("Can not be null nor empty.", "targetState");
            if (sourceState == targetState)
                throw new ArgumentException("Source state and target state must be different.");
            if (transition == null)
                throw new ArgumentNullException("transition");

            sourceState = sourceState.Trim();
            targetState = targetState.Trim();

            // throw if we already have a transition for that state which is not null,
            // null is used to keep track of the last step of the chain
            if (_transitions.ContainsKey(sourceState) && _transitions[sourceState] != null)
                throw new InvalidOperationException(string.Format("A transition from state \"{0}\" has already been defined.",
                    sourceState));

            // register the transition
            _transitions[sourceState] = new YolTransition{
                SourceState = sourceState,
                TargetState = targetState,
                Execute = transition
            };

            // register the target state if we don't know it already
            // this is how we keep track of the final state - because
            // transitions could be defined in any order, that might
            // be overriden afterwards.
            if (!_transitions.ContainsKey(targetState))
                _transitions.Add(targetState, null);

            return this;
        }

        /// <summary>
        /// Initializes the current static unique instance of the <see cref="YolManager"/> class with a state file path and a user login.
        /// </summary>
        /// <param name="statePath">The full path of the file containing the state.</param>
        /// <param name="login">The login of the user to impersonate while running the state machine.</param>
        /// <returns>The current static unique instance of the <see cref="YolManager"/> class.</returns>
        /// <exception cref="ArgumentException"><paramref name="statePath"/> and/or <paramref name="login"/> is <c>null</c> or empty.</exception>
        /// <exception cref="InvalidOperationException">The current static unique instance has already been initialized.</exception>
        public static YolManager Initialize(string statePath, string login)
        {
            if (_current != null)
                throw new InvalidOperationException("Manager has already been initialized.");
            return _current = new YolManager(statePath, login);
        }

        internal static void ExecuteInitialized(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            if (_current != null)
                _current.Execute(umbracoApplication, applicationContext);
        }

        internal void Execute(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            if (umbracoApplication == null)
                throw new ArgumentNullException("umbracoApplication");
            if (applicationContext == null)
                throw new ArgumentNullException("applicationContext");

            ValidateTransitions();

            // impersonate and run - in a using() block to ensure we terminate properly impersonation,
            // whatever happens when executing transitions.
            using (Security.Impersonate(umbracoApplication, applicationContext, _login))
            {
                ExecuteInternal(applicationContext, umbracoApplication.Context.Server);
            }
        }

        // internal for tests
        // beware, that one does not test for graph inconsistencies, should be done beforehand
        // beware, that one does not validate parameters, should be done beforehand
        internal void ExecuteInternal(ApplicationContext applicationContext, HttpServerUtility server)
        {
            LogHelper.Info<YolManager>("Starting...");
            var origState = GetState();
            // ReSharper disable once AccessToModifiedClosure
            LogHelper.Info<YolManager>("At {0}.", () => origState);

            YolTransition transition;
            if (!_transitions.TryGetValue(origState, out transition))
                throw new Exception(string.Format("Unknown state \"{0}\".",
                    origState));

            while (transition != null)
            {
                if (!transition.Execute(applicationContext, server))
                    throw new Exception("Transition failed.");

                var nextState = transition.TargetState;
                SaveState(origState, nextState);
                origState = nextState;

                // ReSharper disable once AccessToModifiedClosure
                LogHelper.Info<YolManager>("At {0}.", () => origState);

                if (!_transitions.TryGetValue(origState, out transition))
                    throw new Exception(string.Format("Unknown state \"{0}\".",
                        origState));
            }
            LogHelper.Info<YolManager>("Done.");
        }

        // internal for tests
        internal void ValidateTransitions()
        {
            // quick check for dead ends - a dead end is a transition that has a target state
            // that is not null and does not match any source state. such a target state has
            // been registered as a source state with a null transition. so there should be only
            // one.
            string finalState = null;
            foreach (var kvp in _transitions.Where(x => x.Value == null))
            {
                if (finalState == null)
                    finalState = kvp.Key;
                else
                    throw new Exception(string.Format("Multiple final states have been detected."));
            }

            // now check for loops
            var verified = new List<string>();
            foreach (var transition in _transitions.Values)
            {
                if (verified.Contains(transition.SourceState)) continue;

                var visited = new List<string> { transition.SourceState };
                var nextTransition = _transitions[transition.TargetState];
                while (nextTransition != null && !verified.Contains(nextTransition.SourceState))
                {
                    if (visited.Contains(nextTransition.SourceState))
                        throw new Exception("A loop has been detected.");
                    visited.Add(nextTransition.SourceState);
                    nextTransition = _transitions[nextTransition.TargetState];
                }
                verified.AddRange(visited);
            }
        }
    }
}
