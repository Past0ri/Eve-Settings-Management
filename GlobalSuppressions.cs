// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using Eve_Settings_Management.Views;
using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Used in MainWindow.axaml", Scope = "member", Target = "~P:Eve_Settings_Management.ViewModels.MainWindowViewModel.FromSelectedItem")]
[assembly: SuppressMessage("Major Code Smell", "S1144:Unused private types or members should be removed", Justification = "Used in MainWindow.axaml", Scope = "member", Target = "~P:Eve_Settings_Management.ViewModels.MainWindowViewModel.TakeBackup")]
[assembly: SuppressMessage("Minor Code Smell", "S3459:Unassigned members should be removed", Justification = "Used in MainWindow.axaml", Scope = "member", Target = "~P:Eve_Settings_Management.ViewModels.MainWindowViewModel.TakeBackup")]
[assembly: SuppressMessage("Minor Code Smell", "S3459:Unassigned members should be removed", Justification = "Used in MainWindow.axaml", Scope = "member", Target = "~P:Eve_Settings_Management.ViewModels.MainWindowViewModel.FromSelectedItem")]