using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharperUniverse.Core
{
    /// <summary>
    /// This is the type the <see cref="GameRunner"/> uses to process commands from the <see cref="IIOHandler"/>. This class cannot be inherited.
    /// </summary>
    public sealed class UniverseCommandRunner
    {
        private readonly List<IUniverseCommandBinding> _bindings;

        /// <summary>
        /// Creates a new instance of <see cref="UniverseCommandRunner"/>.
        /// </summary>
        public UniverseCommandRunner()
        {
            _bindings = new List<IUniverseCommandBinding>();
        }

        /// <summary>
        /// Adds a new <see cref="IUniverseCommandBinding"/> to this command runner.
        /// </summary>
        /// <param name="binding">The binding to add.</param>
        public void AddCommandBinding(IUniverseCommandBinding binding)
        {
            _bindings.Add(binding);
        }

        /// <summary>
        /// Creates a new instance of <see cref="UniverseCommandRunner"/> with a set of commands.
        /// </summary>
        /// <param name="bindings">The command bindings to add.</param>
        public UniverseCommandRunner(List<IUniverseCommandBinding> bindings)
        {
            _bindings = bindings;
        }

        /// <summary>
        /// Asynchronously attempts to execute the specified command from the <see cref="IIOHandler"/>. This should not be called from external code.
        /// </summary>
        /// <param name="command">The name of the command.</param>
        /// <param name="args">The arguments to pass in to the command, in text form.</param>
        /// <returns></returns>
        public async Task AttemptExecuteAsync(string command, List<string> args)
        {
            var bla = _bindings.FirstOrDefault(x => x.CommandName.ToUpper() == command.ToUpper());
            if (bla == null) return;
            await bla.ProcessCommandAsync(args);
        }
    }
}
