﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Threading.Tasks;
using NHibernate;
using NHibernate.Util;

namespace LiquidProjections.NHibernate
{
    internal sealed class NHibernateEventMapConfigurator<TProjection, TKey>
        where TProjection : class, new()
    {
        private readonly Action<TProjection, TKey> setIdentity;
        private readonly IEventMap<NHibernateProjectionContext> map;
        private readonly IEnumerable<INHibernateChildProjector> children;
        private IProjectionCache<TProjection, TKey> cache = new PassthroughCache<TProjection, TKey>();

        public NHibernateEventMapConfigurator(
            IEventMapBuilder<TProjection, TKey, NHibernateProjectionContext> mapBuilder, Action<TProjection, TKey> setIdentity,
            IEnumerable<INHibernateChildProjector> children = null)
        {
            this.setIdentity = setIdentity;
            if (mapBuilder == null)
            {
                throw new ArgumentNullException(nameof(mapBuilder));
            }

            map = BuildMap(mapBuilder);
            this.children = children?.ToList() ?? new List<INHibernateChildProjector>();
        }

        public IProjectionCache<TProjection, TKey> Cache
        {
            get => cache;
            set => cache = value ?? throw new ArgumentNullException(nameof(value));
        }

        private IEventMap<NHibernateProjectionContext> BuildMap(
            IEventMapBuilder<TProjection, TKey, NHibernateProjectionContext> mapBuilder)
        {
            mapBuilder.HandleCustomActionsAs((_, projector) => projector());
            mapBuilder.HandleProjectionModificationsAs(HandleProjectionModification);
            mapBuilder.HandleProjectionDeletionsAs(HandleProjectionDeletion);
            return mapBuilder.Build();
        }

        private async Task HandleProjectionModification(TKey key, NHibernateProjectionContext context,
            Func<TProjection, Task> projector, ProjectionModificationOptions options)
        {
            TProjection projection = await cache.Get(key, () => Task.FromResult(context.Session.Get<TProjection>(key)));
            if (projection == null)
            {
                switch (options.MissingProjectionBehavior)
                {
                    case MissingProjectionModificationBehavior.Create:
                    {
                        projection = new TProjection();
                        setIdentity(projection, key);

                        await projector(projection).ConfigureAwait(false);
                        context.Session.Save(projection);
                        cache.Add(projection);
                        break;
                    }

                    case MissingProjectionModificationBehavior.Ignore:
                    {
                        break;
                    }

                    case MissingProjectionModificationBehavior.Throw:
                    {
                        throw new ProjectionException(
                            $"Projection {typeof(TProjection)} with key {key} does not exist.");
                    }

                    default:
                    {
                        throw new NotSupportedException(
                            $"Not supported missing projection behavior {options.MissingProjectionBehavior}.");
                    }
                }
            }
            else
            {
                context.Session.Lock(projection, LockMode.None);

                switch (options.ExistingProjectionBehavior)
                {
                    case ExistingProjectionModificationBehavior.Update:
                    {
                        await projector(projection).ConfigureAwait(false);
                        break;
                    }

                    case ExistingProjectionModificationBehavior.Ignore:
                    {
                        break;
                    }

                    case ExistingProjectionModificationBehavior.Throw:
                    {
                        throw new ProjectionException(
                            $"Projection {typeof(TProjection)} with key {key} already exists.");
                    }

                    default:
                    {
                        throw new NotSupportedException(
                            $"Not supported existing projection behavior {options.ExistingProjectionBehavior}.");
                    }
                }
            }
        }

        private async Task HandleProjectionDeletion(TKey key, NHibernateProjectionContext context,
            ProjectionDeletionOptions options)
        {
            TProjection existingProjection = 
                await cache.Get(key, () => Task.FromResult(context.Session.Get<TProjection>(key)));

            if (existingProjection == null)
            {
                switch (options.MissingProjectionBehavior)
                {
                    case MissingProjectionDeletionBehavior.Ignore:
                    {
                        break;
                    }

                    case MissingProjectionDeletionBehavior.Throw:
                    {
                        throw new ProjectionException(
                            $"Cannot delete {typeof(TProjection)} projection with key {key}. The projection does not exist.");
                    }

                    default:
                    {
                        throw new NotSupportedException(
                            $"Not supported missing projection behavior {options.MissingProjectionBehavior}.");
                    }
                }
            }
            else
            {
                context.Session.Delete(existingProjection);
                cache.Remove(key);
            }
        }

        public async Task ProjectEvent(object anEvent, NHibernateProjectionContext context)
        {
            foreach (INHibernateChildProjector child in children)
            {
                await child.ProjectEvent(anEvent, context).ConfigureAwait(false);
            }

            await map.Handle(anEvent, context).ConfigureAwait(false);
        }
    }
}