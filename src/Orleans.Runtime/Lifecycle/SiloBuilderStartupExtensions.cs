﻿using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Orleans.Runtime;
using Orleans.Runtime.Scheduler;

namespace Orleans.Hosting
{
    /// <summary>
    /// The silo builder startup extensions.
    /// </summary>
    public static class SiloBuilderStartupExtensions
    {
        /// <summary>
        /// Adds a startup task to be executed when the silo has started.
        /// </summary>
        /// <param name="builder">
        /// The builder.
        /// </param>
        /// <param name="stage">
        /// The stage to execute the startup task, see values in <see cref="SiloLifecycleStage"/>.
        /// </param>
        /// <typeparam name="TStartup">
        /// The startup task type.
        /// </typeparam>
        /// <returns>
        /// The provided <see cref="ISiloHostBuilder"/>.
        /// </returns>
        public static ISiloHostBuilder AddStartupTask<TStartup>(
            this ISiloHostBuilder builder,
            int stage = SiloLifecycleStage.SiloActive)
            where TStartup : class, IStartupTask
        {
            return builder.AddStartupTask((sp, ct) => ActivatorUtilities.GetServiceOrCreateInstance<TStartup>(sp).Execute(ct), stage);
        }

        /// <summary>
        /// Adds a startup task to be executed when the silo has started.
        /// </summary>
        /// <param name="builder">
        /// The builder.
        /// </param>
        /// <param name="startupTask">
        /// The startup task.
        /// </param>
        /// <param name="stage">
        /// The stage to execute the startup task, see values in <see cref="SiloLifecycleStage"/>.
        /// </param>
        /// <returns>
        /// The provided <see cref="ISiloHostBuilder"/>.
        /// </returns>
        public static ISiloHostBuilder AddStartupTask(
            this ISiloHostBuilder builder,
            IStartupTask startupTask,
            int stage = SiloLifecycleStage.SiloActive)
        {
            return builder.AddStartupTask((sp, ct) => startupTask.Execute(ct), stage);
        }

        /// <summary>
        /// Adds a startup task to be executed when the silo has started.
        /// </summary>
        /// <param name="builder">
        /// The builder.
        /// </param>
        /// <param name="startupTask">
        /// The startup task.
        /// </param>
        /// <param name="stage">
        /// The stage to execute the startup task, see values in <see cref="SiloLifecycleStage"/>.
        /// </param>
        /// <returns>
        /// The provided <see cref="ISiloHostBuilder"/>.
        /// </returns>
        public static ISiloHostBuilder AddStartupTask(
            this ISiloHostBuilder builder,
            Func<IServiceProvider, CancellationToken, Task> startupTask,
            int stage = SiloLifecycleStage.SiloActive)
        {
            builder.ConfigureServices(services =>
                services.AddTransient<ILifecycleParticipant<ISiloLifecycle>>(sp =>
                    new StartupTask(
                        sp,
                        sp.GetRequiredService<OrleansTaskScheduler>(),
                        sp.GetRequiredService<StartupTaskSystemTarget>(),
                        startupTask,
                        stage)));
            return builder;
        }

        /// <inheritdoc />
        private class StartupTask : ILifecycleParticipant<ISiloLifecycle>
        {
            private readonly IServiceProvider serviceProvider;
            private readonly OrleansTaskScheduler scheduler;
            private readonly StartupTaskSystemTarget schedulingTarget;
            private readonly Func<IServiceProvider, CancellationToken, Task> startupTask;

            private readonly int stage;

            public StartupTask(
                IServiceProvider serviceProvider,
                OrleansTaskScheduler scheduler,
                StartupTaskSystemTarget schedulingTarget,
                Func<IServiceProvider, CancellationToken, Task> startupTask,
                int stage)
            {
                this.serviceProvider = serviceProvider;
                this.scheduler = scheduler;
                this.schedulingTarget = schedulingTarget;
                this.startupTask = startupTask;
                this.stage = stage;
            }

            /// <inheritdoc />
            public void Participate(ISiloLifecycle lifecycle)
            {
                lifecycle.Subscribe(
                    this.stage,
                    cancellation => this.scheduler.QueueTask(
                        () => this.startupTask(this.serviceProvider, cancellation),
                        this.schedulingTarget.SchedulingContext));
            }
        }
    }
}