using System;
using OmiLAXR.Schedules;
using UnityEngine;

namespace OmiLAXR.TrackingBehaviours
{
    public abstract class ScheduledTrackingBehaviour : ScheduledTrackingBehaviour<Component>
    {
        protected override void AfterFilteredObjects(Component[] components)
        {
            
        }
    }
    /// <summary>
    /// Base class for tracking behaviors that operate on a timed interval
    /// rather than processing every frame. Derived classes should implement
    /// ProcessObjectsAtInterval to define the interval-based behavior.
    /// </summary>
    public abstract class ScheduledTrackingBehaviour<T> : TrackingBehaviour<T>
        where T : Component
    {
        [Tooltip("Decides how often Run is called. Empty uses a copy of the scheduler in GlobalSettings.")]
        public Scheduler scheduler;

        protected virtual Scheduler GetDefaultScheduler() => GlobalSettings.Instance.GetScheduler().Clone(this);

        protected override void OnStartedPipeline(Pipeline pipeline)
        {
            if (!enabled)
                return;
            
            if (!scheduler)
                scheduler = GetDefaultScheduler();
            
            scheduler.OnTick += Run;
            Schedulers.Add(scheduler);
            
            if (!scheduler.owner)
                scheduler.owner = this;
            
            scheduler.Start();
            
            base.OnStartedPipeline(pipeline);
        }

        protected abstract void Run();
    }
}