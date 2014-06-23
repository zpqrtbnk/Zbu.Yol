using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web.Hosting;
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
        private readonly bool _nodb;
        private string _statePath; // temp for compat.
        private readonly string _name;
        private readonly string _login;

        private readonly Dictionary<string, YolTransition> _transitions
            = new Dictionary<string, YolTransition>(StringComparer.InvariantCultureIgnoreCase);
        private static readonly Dictionary<string, YolManager> Managers
            = new Dictionary<string, YolManager>(StringComparer.InvariantCultureIgnoreCase);

        // internal for tests
        internal YolManager()
        {
            _nodb = true;
            _name = null; // default
            _login = null; // cannot impersonate
            State = string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YolManager"/> class with a state file path and a user login.
        /// </summary>
        /// <param name="name">The name of the manager.</param>
        /// <param name="login">The login of the user to impersonate while running the state machine.</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> and/or <paramref name="login"/> is <c>null</c> or empty.</exception>
        internal YolManager(string name, string login)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Cannot be null nor empty.", "name");
            if (string.IsNullOrWhiteSpace(login))
                throw new ArgumentException("Cannot be null nor empty.", "login");

            _name = name;
            _login = login;
        }

        #region State Management

        // for tests
        internal string State { get; private set; }

        private string GetState()
        {
            return _nodb ? State : ZbuKeyValueStore.GetValue("Zbu.Yol.Manager." + _name + ".State");
        }

        private void SaveState(string origState, string state)
        {
            if (_nodb)
                State = state;
            else
                ZbuKeyValueStore.SetValue("Zbu.Yol.Manager." + _name + ".State", origState, state);
        }

        #endregion

        #region Creation

        // for backward compatibility only!
        public static YolManager Initialize(string statePath, string login)
        {
            var manager = Create(login);
            manager._statePath = statePath;
            return manager;
        }

        /// <summary>
        /// Creates and registers an instance of the <see cref="YolManager"/> class with a name and a user login.
        /// </summary>
        /// <param name="name">The name of the manager.</param>
        /// <param name="login">The login of the user to impersonate while running the state machine.</param>
        /// <returns>The instance of the <see cref="YolManager"/> class.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> and/or <paramref name="login"/> is <c>null</c> or empty.</exception>
        /// <exception cref="InvalidOperationException">A manager with that name has already been initialized.</exception>
        public static YolManager Create(string name, string login)
        {
            lock (Managers)
            {
                if (Managers.ContainsKey(name))
                    throw new InvalidOperationException(string.Format("A manager named \"{0}\" has already been initialized.", name));
                return Managers[name] = new YolManager(name, login);
            }
        }

        /// <summary>
        /// Creates and registers an instance of the <see cref="YolManager"/> class with a name.
        /// </summary>
        /// <param name="name">The name of the manager.</param>
        /// <returns>The instance of the <see cref="YolManager"/> class.</returns>
        /// <remarks>The login will come from appSettings with key "Zbu.Yol.Manager.{name}.Login".</remarks>
        /// <exception cref="ArgumentException"><paramref name="name"/> is <c>null</c> or empty.</exception>
        /// <exception cref="InvalidOperationException">A manager with that name has already been initialized.</exception>
        /// <exception cref="InvalidOperationException">No proper login was found in appSettings.</exception>
        public static YolManager Create(string name)
        {
            var login = ConfigurationManager.AppSettings["Zbu.Yol.Manager." + name + ".Login"];
            if (string.IsNullOrWhiteSpace(login))
                throw new InvalidOperationException(string.Format("No login found in appSettings for name \"{0}\".", name));
            return Create(name, login);
        }

        /// <summary>
        /// Creates and registers the default instance of the <see cref="YolManager"/> class with a login.
        /// </summary>
        /// <param name="login">The login of the user to impersonate while running the state machine.</param>
        /// <returns>The default instance of the <see cref="YolManager"/> class.</returns>
        /// <exception cref="ArgumentException"><paramref name="login"/> is <c>null</c> or empty.</exception>
        /// <exception cref="InvalidOperationException">The default manager has already been initialized.</exception>
        public static YolManager CreateDefault(string login)
        {
            return Create("Default", login);
        }

        /// <summary>
        /// Creates and registers the default instance of the <see cref="YolManager"/>.
        /// </summary>
        /// <returns>The default instance of the <see cref="YolManager"/> class.</returns>
        /// <remarks>The login will come from appSettings with key "Zbu.Yol.Manager.Default.Login".</remarks>
        /// <exception cref="InvalidOperationException">The default manager has already been initialized.</exception>
        /// <exception cref="InvalidOperationException">No proper login was found in appSettings.</exception>
        public static YolManager CreateDefault()
        {
            return Create("Default");
        }

        #endregion

        #region Transitions

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
        public YolManager DefineTransition(string sourceState, string targetState, Func<bool> transition)
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
            _transitions[sourceState] = new YolTransition
            {
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
                if (transition == null || verified.Contains(transition.SourceState)) continue;

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

        #endregion

        #region Execute

        internal static void ExecuteInitialized(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            lock (Managers)
            {
                foreach (var manager in Managers.Values)
                    manager.Execute(umbracoApplication, applicationContext);
            }
        }

        internal void Execute(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            if (umbracoApplication == null)
                throw new ArgumentNullException("umbracoApplication");
            if (applicationContext == null)
                throw new ArgumentNullException("applicationContext");

            // temp for compat
            if (!_nodb)
            {
                var state = ZbuKeyValueStore.GetValue("Zbu.Yol.Manager." + _name + ".State");
                if (string.IsNullOrWhiteSpace(state) || !string.IsNullOrWhiteSpace(_statePath))
                {
                    try
                    {
                        state = File.ReadAllText(umbracoApplication.Server.MapPath(_statePath));
                    }
                    catch (Exception)
                    {
                        state = null;
                    }
                    if (!string.IsNullOrWhiteSpace(state))
                        ZbuKeyValueStore.SetValue("Zbu.Yol.Manager." + _name + ".State", state);
                }
            }

            ValidateTransitions();

            // impersonate and run - in a using() block to ensure we terminate properly impersonation,
            // whatever happens when executing transitions.
            using (Security.Impersonate(umbracoApplication, applicationContext, _login))
            {
                ExecuteInternal();
            }
        }

        // internal for tests
        // beware, that one does not test for graph inconsistencies, should be done beforehand
        // beware, that one does not validate parameters, should be done beforehand
        internal void ExecuteInternal()
        {
            LogHelper.Info<YolManager>("Starting \"{0}\"...", () => _name);
            var origState = GetState();
            // ReSharper disable once AccessToModifiedClosure
            LogHelper.Info<YolManager>("At {0}.", () => origState);

            YolTransition transition;
            if (!_transitions.TryGetValue(origState, out transition))
                throw new Exception(string.Format("Unknown state \"{0}\".",
                    origState));

            while (transition != null)
            {
                if (!transition.Execute())
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

        #endregion
    }
}
