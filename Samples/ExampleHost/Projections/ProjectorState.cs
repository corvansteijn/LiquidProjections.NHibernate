using System;
using FluentNHibernate.Automapping;
using FluentNHibernate.Automapping.Alterations;
using FluentNHibernate.Mapping;
using LiquidProjections.NHibernate;

namespace LiquidProjections.ExampleHost.Projections
{
    public class ProjectorState : IProjectorState, IPersistable
    {
        public virtual string Id { get; set; }
        public virtual long Checkpoint { get; set; }
        public virtual DateTime LastUpdateUtc { get; set; }

        public virtual string LastStreamId { get; set; }
    }

    public class ProjectorStateMappingOverride : IAutoMappingOverride<ProjectorState>
    {
        public void Override(AutoMapping<ProjectorState> mapping)
        {
            mapping.Id(x => x.Id).Not.Nullable().Length(150);
            mapping.Map(x => x.Checkpoint).Column("TheCheckpoint");
            mapping.Map(x => x.LastUpdateUtc);
            mapping.Map(x => x.LastStreamId);
        }
    }
}