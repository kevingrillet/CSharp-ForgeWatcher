using CSharp_AzureDevopsNotifier.Entities;
using CSharp_AzureDevopsNotifier.Helpers;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;

namespace CSharp_AzureDevopsNotifier.Forms
{
    [ExcludeFromCodeCoverage]
    public class TrayApplicationContext : ApplicationContext
    {
        private const string _pathSettings = @"Configurations\AzureDevOpsSettings.json";
        private readonly NotifyIcon _notificationIcon;
        private EditForm _editForm;
        private AzureDevOpsManager _manager;
        private AzureDevOpsSettings _settings;

        public TrayApplicationContext()
        {
            _notificationIcon = new NotifyIcon()
            {
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true
            };
            SetTrayIcon();
            Refresh(null, null);
        }

        /// <summary>
        /// Open Form to edit config file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Edit(object sender, EventArgs e)
        {
            // Only one edit window at a time; bring it to front if already open.
            if (_editForm != null && !_editForm.IsDisposed)
            {
                _editForm.Activate();
                return;
            }

            _editForm = new EditForm(_pathSettings, _settings);
            _editForm.FormClosed += (sender, e) => { Refresh(null, null); };
            _editForm.Show();
        }

        /// <summary>
        /// Exit app
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Exit(object sender, EventArgs e)
        {
            _notificationIcon.Visible = false;
            Application.Exit();
        }

        /// <summary>
        /// Load _settings from Json. Set Icons. Refresh Menus.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Refresh(object sender, EventArgs e)
        {
            try
            {
                // Load config file
                _settings = JsonHelpers<AzureDevOpsSettings>.Load(_pathSettings);

                if (_manager != null)
                    _manager.Update(_settings);
                else
                    _manager = new AzureDevOpsManager(_settings);
                _ = _manager.RunAsync();
            }
            catch (Exception ex)
            {
                // Bad / missing config must not crash the tray app at startup or on refresh.
                ToastHelpers.ShowToastNotification("AzureDevopsNotifier - config error", ex.Message, null);
            }
        }

        private void SetTrayIcon()
        {
            // Set Tray icon
            _notificationIcon.Icon = new Icon(@"Ressources/Icons/Microsoft-Azure.ico");

            // Update menus
            var contextMenuStrip = _notificationIcon.ContextMenuStrip;
            contextMenuStrip.Items.Clear();

            // Static bottom menus
            contextMenuStrip.Items.AddRange(new ToolStripItem[] {
                new ToolStripMenuItem(nameof(Refresh), null, new EventHandler(Refresh)),
                new ToolStripMenuItem(nameof(Edit), null, new EventHandler(Edit)),
                new ToolStripMenuItem(nameof(Exit), null, new EventHandler(Exit)),
            });
        }
    }
}
