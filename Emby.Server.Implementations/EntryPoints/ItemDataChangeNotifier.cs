#pragma warning disable CS1591

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Data.Events;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.EntryPoints
{
    public class ItemDataChangedNotifier : IServerEntryPoint
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ISessionManager _sessionManager;
        private readonly IUserManager _userManager;
        private readonly ILogger<ItemDataChangedNotifier> _logger;
        /// <summary>
        /// Lock for item data change notifications
        /// </summary>
        private readonly object _libraryChangedSyncLock = new object();
        private readonly List<BaseItem> _itemsUpdated = new List<BaseItem>();

        public ItemDataChangedNotifier(
            ILibraryManager libraryManager,
            ISessionManager sessionManager,
            IUserManager userManager,
            ILogger<ItemDataChangedNotifier> logger,
            IProviderManager providerManager)
        {
            _libraryManager = libraryManager;
            _sessionManager = sessionManager;
            _userManager = userManager;
            _logger = logger;
        }

        public Task RunAsync()
        {
            _libraryManager.ItemUpdated += OnLibraryItemUpdated;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles the ItemUpdated event of the libraryManager control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ItemChangeEventArgs"/> instance containing the event data.</param>
        private async void OnLibraryItemUpdated(object sender, ItemChangeEventArgs e)
        {
            var userIds = _sessionManager.Sessions
            .Select(i => i.UserId)
            .Where(i => !i.Equals(Guid.Empty))
            .Distinct()
            .ToArray();

            var dict = new Dictionary<string, string>();
            dict["ItemId"] = e.Item.Id.ToString("N", CultureInfo.InvariantCulture);

            foreach (var userId in userIds)
            {
                try
                {
                    var user = _userManager.GetUserById(userId);

                    await _sessionManager.SendMessageToUserSessions(new List<Guid> { userId }, SessionMessageType.ItemDataChanged, dict, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending LibraryChanged message");
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                _libraryManager.ItemUpdated -= OnLibraryItemUpdated;
            }
        }
    }
}
