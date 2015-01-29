﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Caching;

namespace Umbraco.Web.Cache
{
    public sealed class ContentCacheRefresher : JsonCacheRefresherBase<ContentCacheRefresher>
    {
        #region Json

        // ContentCacheRefresher is the result of the merge of PageCacheRefresher and
        // UnpublishedPageCacheRefresher - because they have to work together as one cache

        // each content HAS a NEWEST version, and MAY have a PUBLISHED version
        // they MAY be the same version, else the NEWEST version is the most recent of both

        // when... triger...
        //
        // Save, Rollback: RefreshNewest
        // Publish: RefreshNewest + RefreshPublished -- RefreshNewest since newest changes & is now published
        // Save&Publish: RefreshNewest + RefreshPublished
        // Unpublish: RefreshNewest + RemovePublished
        // (republished): RefreshPublished -- when it's "published again" because of parents
        // Delete: RemoveNewest + RemovePublished
        // Sort: RefreshNewest [+ RefreshPublished] -- if there's a published version, refresh it
        // Move: RefreshNewest [+ RefreshPublished] -- fixme what shall we do
        // Cancel: RefreshNewest -- when cancelling changes & coming back to published

        // RefreshPublished
        // - Examine should update indexers that do NOT support unpublished content w/published version
        // - IContent cache does nothing
        // - PublishedContent cache should refresh the published content w/published version
        // RefreshNewest
        // - Examine should update indexers that DO support unpublished content w/newest version
        // - IContent cache should clear the content
        // - PublishedContent cache should refresh the preview content w/newest version
        //   if that version is not published, else it should clear the preview content
        // RemovePublished
        // - Examine should clear indexers that do NOT support unpublished content
        // - IContent cache does nothing
        // - PublishedContent cache should clear the published content
        // RemoveNewest
        // - Examine should clear indexers that DO support unpublished content
        // - IContent cache should clear the content
        // - PublishedContent cache should clear the preview content

        [Flags]
        internal enum JsonAction
        {
            None = 0,
            RefreshPublished = 1,
            RefreshAllPublished = 2,
            RemovePublished = 4,
            RefreshNewest = 8,
            RefreshAllNewest = 16,
            RemoveNewest = 32
        }

        internal static bool HasFlagAny(JsonAction action, JsonAction actions)
        {
            return (action & actions) != JsonAction.None;
        }

        internal class JsonPayload
        {
            public JsonPayload(int id, JsonAction action)
            {
                Id = id;
                Action = action;
            }

            public int Id { get; private set; }
            public JsonAction Action { get; private set; }
        }

        internal static string Serialize(IEnumerable<JsonPayload> payloads)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(payloads.ToArray());
        }

        internal static JsonPayload[] Deserialize(string json)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<JsonPayload[]>(json);
        }

        #endregion

        #region Define

        protected override ContentCacheRefresher Instance
        {
            get { return this; }
        }

        public override Guid UniqueIdentifier
        {
            get { return DistributedCache.ContentCacheRefresherGuid; }
        }

        public override string Name
        {
            get { return "ContentCacheRefresher"; }
        }
        
        #endregion

        #region Events

        public override void Refresh(string json)
        {
            foreach (var payload in Deserialize(json))
            {
                if (HasFlagAny(payload.Action, JsonAction.RefreshPublished | JsonAction.RemovePublished | JsonAction.RefreshAllPublished))
                {
                    // from PageCacheRefresher
                    ApplicationContext.Current.ApplicationCache.ClearPartialViewCache();
                    DistributedCache.Instance.ClearAllMacroCacheOnCurrentServer();
                    DistributedCache.Instance.ClearXsltCacheOnCurrentServer();
                }

                if (HasFlagAny(payload.Action, JsonAction.RefreshNewest | JsonAction.RemoveNewest | JsonAction.RefreshAllNewest))
                {
                    // from UnpublishedPageCacheRefresher
                    RuntimeCacheProvider.Current.Delete(typeof(IContent), payload.Id);
                }
            }

            base.Refresh(json);
        }

        // these events should never trigger
        // everything should be JSON

        public override void RefreshAll()
        {
            throw new NotSupportedException();
        }

        public override void Refresh(int id)
        {
            throw new NotSupportedException();
        }

        public override void Refresh(Guid id)
        {
            throw new NotSupportedException();
        }

        public override void Remove(int id)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}
