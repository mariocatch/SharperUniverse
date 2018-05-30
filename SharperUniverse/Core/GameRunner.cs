using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharperUniverse.Utilities;

namespace SharperUniverse.Core
{

    public class GameRunnerBuilder
    {
        private GameRunner _gameRunner;

        public GameRunnerBuilder()
        {
            _gameRunner = new GameRunner();
            _gameRunner.CommandRunner = new UniverseCommandRunner();
        }

        public GameRunnerBuilder AddSystem<TSystem, TComponent>(int numEntities = 1) where TComponent : BaseSharperComponent
        {
            BaseSharperSystem<TComponent> system = null;

            var systemConstructors = typeof(TSystem).GetConstructors();
            foreach (var systemConstructor in systemConstructors)
            {
                var parameters = systemConstructor.GetParameters();
                List<object> builtParameters = new List<object>();

                foreach (var parameter in parameters)
                {
                    if (parameter.ParameterType == typeof(GameRunner))
                    {
                        builtParameters.Add(_gameRunner);
                    }
                    else if (typeof(BaseSharperSystem<>).IsSubclassOfRawGeneric(parameter.ParameterType))
                    {
                        builtParameters.Add(_gameRunner.Systems.Single(s => s.GetType() == parameter.ParameterType));
                    }
                }
                system = (BaseSharperSystem<TComponent>)systemConstructor.Invoke(builtParameters.ToArray());
                break;
            }

            for (int i = 0; i < numEntities; i++)
            {
                var entity = _gameRunner.CreateEntityAsync().GetAwaiter().GetResult();
                system.RegisterComponentAsync(entity).GetAwaiter().GetResult();
            }

            return this;
        }

        public GameRunnerBuilder WithFrameRate(int fps)
        {
            _gameRunner.DeltaMs = fps;
            return this;
        }

        public GameRunnerBuilder AddIOHandler<T>() where T : IIOHandler
        {
            if (_gameRunner.IOHandler != null) throw new InvalidOperationException("You may only add one IOHandler to the GameRunner");

            _gameRunner.IOHandler = Activator.CreateInstance<T>();
            return this;
        }

        public GameRunnerBuilder AddCommand<T>(string commandName) where T : IUniverseCommandBinding
        {
            IUniverseCommandBinding binding = null;

            var commandBindingConstructors = typeof(T).GetConstructors();
            foreach (var commandBindingConstructor in commandBindingConstructors)
            {
                var parameters = commandBindingConstructor.GetParameters();
                List<object> builtParameters = new List<object>();

                foreach (var parameter in parameters)
                {
                    if (parameter.ParameterType == typeof(string))
                    {
                        builtParameters.Add(commandName);
                    }
                    else if (parameter.ParameterType == typeof(IIOHandler))
                    {
                        builtParameters.Add(_gameRunner.IOHandler);
                    }
                    else if (typeof(BaseSharperSystem<>).IsSubclassOfRawGeneric(parameter.ParameterType))
                    {
                        builtParameters.Add(_gameRunner.Systems.Single(s => s.GetType() == parameter.ParameterType));
                    }
                }
                binding = (T)commandBindingConstructor.Invoke(builtParameters.ToArray());
                break;
            }

            _gameRunner.CommandRunner.AddCommandBinding(binding);

            return this;
        }

        public GameRunner Build()
        {
            return _gameRunner;
        }
    }

    /// <summary>
    /// This is the entry point and main manager for any game that uses SharperUniverse.
    /// </summary>
    public class GameRunner
    {
        internal readonly List<ISharperSystem<BaseSharperComponent>> Systems;
        internal UniverseCommandRunner CommandRunner;
        internal IIOHandler IOHandler;
        private readonly List<SharperEntity> _entities;
        internal int DeltaMs;

        public GameRunner()
        {
            Systems = new List<ISharperSystem<BaseSharperComponent>>();
            _entities = new List<SharperEntity>();
            DeltaMs = 50;
        }

        /// <summary>
        /// Creates a new instance of <see cref="GameRunner"/> with the specified <see cref="UniverseCommandRunner"/> and <see cref="IIOHandler"/> instances, along with the delta time, in milliseconds.
        /// </summary>
        /// <param name="commandRunner">Your instance of <see cref="UniverseCommandRunner"/>.</param>
        /// <param name="ioHandler">Your implementation of the IO logic.</param>
        /// <param name="deltaMs">The frequency of the update cycle, in milliseconds.</param>
        public GameRunner(UniverseCommandRunner commandRunner, IIOHandler ioHandler, int deltaMs)
        {
            Systems = new List<ISharperSystem<BaseSharperComponent>>();
            CommandRunner = commandRunner;
            IOHandler = ioHandler;
            _entities = new List<SharperEntity>();
            DeltaMs = deltaMs;
        }

        /// <summary>
        /// Registers an <see cref="ISharperSystem{T}"/> to the Game. This should not be called from external code.
        /// </summary>
        /// <param name="system">The target system to register.</param>
        public GameRunner RegisterSystem(ISharperSystem<BaseSharperComponent> system)
        {
            if (Systems.Contains(system) || Systems.Any(x => x.GetType() == system.GetType())) throw new DuplicateSharperObjectException();

            Systems.Add(system);
            return this;
        }

        public GameRunner RegisterSystem<T>() where T : ISharperSystem<BaseSharperComponent>
        {
            return RegisterSystem(Activator.CreateInstance<T>());
        }

        /// <summary>
        /// Asynchronously creates a new <see cref="SharperEntity"/> and registers it to the game.
        /// </summary>
        /// <returns>A <see cref="Task{TResult}"/> that will contain the new <see cref="SharperEntity"/> on completion.</returns>
        public Task<SharperEntity> CreateEntityAsync()
        {
            var ent = new SharperEntity();
            _entities.Add(ent);
            return Task.FromResult(ent); //I have no idea if this is ok LUL
        }

        /// <summary>
        /// Launches the Game. This task runs for as long as the game is running.
        /// </summary>
        /// <returns>A <see cref="Task"/> represnting the asynchronous game loop.</returns>
        public async Task RunGameAsync()
        {
            Task<(string commandName, List<string> args)> inputTask = Task.Run(() => IOHandler.GetInputAsync());
            Func<string, Task> outputDel = IOHandler.SendOutputAsync;
            while (true)
            {
                if (inputTask.IsCompleted)
                {
                    await CommandRunner.AttemptExecuteAsync(inputTask.Result.commandName, inputTask.Result.args);
                    inputTask = Task.Run(() => IOHandler.GetInputAsync());
                }
                else if (inputTask.IsFaulted)
                {
                    var exception = inputTask.Exception ?? new Exception("REEEEEEEEEEEEEEEEEEE WTF DID YOU DO TO MY POOR ENGINE???");
                    throw exception;
                }
                foreach (var sharperSystem in Systems)
                {
                    await sharperSystem.CycleUpdateAsync(outputDel);
                }
                await Task.Delay(DeltaMs);
            }
        }
    }
}
