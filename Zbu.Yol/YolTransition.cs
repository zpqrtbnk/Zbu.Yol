using System;
using System.Web;
using Umbraco.Core;

namespace Zbu.Yol
{
    /// <summary>
    /// Represents a transition between two states.
    /// </summary>
    public class YolTransition
    {
        // ensure it cannot be created outside of the assembly
        internal YolTransition()
        { }

        /// <summary>
        /// Gets or sets the source state of the transition.
        /// </summary>
        public string SourceState { get; internal set; }

        /// <summary>
        /// Gets or sets the target state of the transition.
        /// </summary>
        public string TargetState { get; internal set; }

        /// <summary>
        /// Gets or sets the function to execute with the transition.
        /// </summary>
        public Func<ApplicationContext, HttpServerUtility, bool> Execute { get; internal set; }
    }
}
