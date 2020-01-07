using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;

namespace CKAN
{
    public partial class Main
    {

        private List<ModChange> changeSet;

        public void UpdateChangesDialog(List<ModChange> changeset, BackgroundWorker installWorker)
        {
            changeSet = changeset;
            this.installWorker = installWorker;
            ChangesListView.Items.Clear();

            if (changeset == null)
            {
                return;
            }

            // We're going to split our change-set into two parts: updated/removed mods,
            // and everything else (which right now is replacing and installing mods, but we may have
            // other types in the future).

            changeSet = new List<ModChange>();
            changeSet.AddRange(changeset.Where(change => change.ChangeType == GUIModChangeType.Remove));
            changeSet.AddRange(changeset.Where(change => change.ChangeType == GUIModChangeType.Update));

            IEnumerable<ModChange> leftOver = changeset.Where(change => change.ChangeType != GUIModChangeType.Remove
                                                && change.ChangeType != GUIModChangeType.Update);

            // Now make our list more human-friendly (dependencies for a mod are listed directly
            // after it.)
            CreateSortedModList(leftOver);

            changeset = changeSet;

            foreach (var change in changeset)
            {
                if (change.ChangeType == GUIModChangeType.None)
                {
                    continue;
                }

                CkanModule m = change.Mod;
                ModuleLabel warnLbl = FindLabelAlertsBeforeInstall(m);
                ChangesListView.Items.Add(new ListViewItem(new string[]
                {
                    change.NameAndStatus,
                    change.ChangeType.ToString(),
                    warnLbl != null
                        ? string.Format(
                            Properties.Resources.MainChangesetWarningInstallingModuleWithLabel,
                            warnLbl.Name,
                            change.Description
                          )
                        : change.Description
                })
                {
                    Tag = m,
                    ForeColor = warnLbl != null ? Color.Red : SystemColors.WindowText
                });
            }
        }

        private void ClearChangeSet()
        {
            foreach (DataGridViewRow row in mainModList.full_list_of_mod_rows.Values)
            {
                GUIMod mod = row.Tag as GUIMod;
                if (mod.IsInstallChecked != mod.IsInstalled)
                {
                    mod.SetInstallChecked(row, Installed, mod.IsInstalled);
                }
                mod.SetUpgradeChecked(row, UpdateCol, false);
                mod.SetReplaceChecked(row, ReplaceCol, false);
            }
        }

        /// <summary>
        /// This method creates the Install part of the changeset
        /// It arranges the changeset in a human-friendly order
        /// The requested mod is listed first, it's dependencies right after it
        /// So we get for example "ModuleRCSFX" directly after "USI Exploration Pack"
        ///
        /// It is very likely that this is forward-compatible with new ChangeTypes's,
        /// like a "reconfigure" changetype, but only the future will tell
        /// </summary>
        /// <param name="changes">Every leftover ModChange that should be sorted</param>
        /// <param name="parent"></param>
        private void CreateSortedModList(IEnumerable<ModChange> changes, ModChange parent=null)
        {
            foreach (ModChange change in changes)
            {
                bool goDeeper = parent == null || change.Reason.Parent.identifier == parent.Mod.identifier;

                if (goDeeper)
                {
                    if (!changeSet.Any(c => c.Mod.identifier == change.Mod.identifier && c.ChangeType != GUIModChangeType.Remove))
                        changeSet.Add(change);
                    CreateSortedModList(changes.Where(c => !(c.Reason is SelectionReason.UserRequested)), change);
                }
            }
        }

        private void CancelChangesButton_Click(object sender, EventArgs e)
        {
            ClearChangeSet();
            UpdateChangesDialog(null, installWorker);
            tabController.ShowTab("ManageModsTabPage");
        }

        private void ConfirmChangesButton_Click(object sender, EventArgs e)
        {
            if (changeSet == null)
                return;

            menuStrip1.Enabled = false;
            RetryCurrentActionButton.Visible = false;

            //Using the changeset passed in can cause issues with versions.
            // An example is Mechjeb for FAR at 25/06/2015 with a 1.0.2 install.
            // TODO Work out why this is.
            installWorker.RunWorkerAsync(
                new KeyValuePair<List<ModChange>, RelationshipResolverOptions>(
                    mainModList.ComputeUserChangeSet(RegistryManager.Instance(Main.Instance.CurrentInstance).registry).ToList(),
                    RelationshipResolver.DependsOnlyOpts()
                )
            );
        }

    }
}
