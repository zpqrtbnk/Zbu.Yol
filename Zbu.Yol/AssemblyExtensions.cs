using System;
using System.IO;
using System.Reflection;

namespace Zbu.Yol
{
    /// <summary>
    /// Provides extension methods to the <see cref="Assembly"/> class.
    /// </summary>
    internal static class AssemblyExtensions
    {
        /// <summary>
        /// Gets the text content of an assembly resource identified by its name.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <param name="name">The resource name.</param>
        /// <returns>The text content of the resource.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="name"/> is <c>null</c>, empty, or not a valid resource name within the assembly.</exception>
        public static string GetManifestResourceText(this Assembly assembly, string name)
        {
            if (assembly == null)
                throw new ArgumentNullException("assembly");
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Cannot be null nor empty.", "name");

            using (var stream = assembly.GetManifestResourceStream(name))
            {
                if (stream == null)
                    throw new ArgumentException(string.Format("Not a valid resource name within assembly \"{0}\": \"{1}\".",
                        assembly.FullName, name));

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
