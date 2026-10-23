using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Everest.Menus.Chapters;
using Celeste.Everest.Content.Registry;
using Celeste.Everest.UI.Menus.Files;
using Celeste.Game.Execution.Session;
using Celeste.Core.Scheduling;
using Celeste.Mods.Tas;
using Celeste.Game.Execution.Areas;
using Celeste.Everest.Content.Maps.Registry;
using Celeste.Everest.Menus.MapList;
using Celeste.Everest.Menus.MapSearch;
using Celeste.Mods.CollabUtils2.Maps;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CollabUtils2.UI.MapMenus
{
    public sealed class CollabMapMenuHooks : IDisposable
    {
        private static readonly FieldInfo listEngine = typeof(EverestMapListSequence)
            .GetField("engine", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo searchEngine = typeof(EverestMapSearchSequence)
            .GetField("engine", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly ISourceEngineContext engine;
        private readonly CollabIdentityRegistry identities;
        private HelperTestMapVisibility helperTests;
        private ILHook listItems;
        private ILHook listMenu;
        private ILHook searchItems;
        private ILHook chapterPicker;
        private ILHook searchMenu;
        private ILHook chapterPickerEntry;
        private ILHook filePicker;
        private ILHook tasCampaignSelection;
        private ILHook tasCampaignValidation;
        private ILHook tasCampaignCompletions;

        public CollabMapMenuHooks(ISourceEngineContext engine, CollabIdentityRegistry identities)
        {
            this.engine = engine;
            this.identities = identities;
            helperTests =
                new HelperTestMapVisibility(((IEverestContentRegistryEnvironment)engine).SourceContentRegistry);
        }

        public void Load()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            listItems = new ILHook(typeof(EverestMapListSequence).GetMethod("ReloadItems", flags),
                il => PatchMapItems(il, listEngine));
            listMenu = new ILHook(typeof(EverestMapListSequence).GetMethod("CreateMenu", flags), PatchLevelSets);
            searchItems = new ILHook(typeof(EverestMapSearchSequence).GetMethod("ReloadItems", flags),
                il => PatchMapItems(il, searchEngine));
            var entry = typeof(EverestChapterLevelSetPickerSequence).GetMethod("Enter");
            var iterator = entry.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            chapterPicker = new ILHook(iterator.GetMethod("MoveNext", flags),
                il => PatchChapterPicker(il, iterator.GetField("<>4__this")));
            searchMenu = new ILHook(typeof(EverestMapSearchSequence).GetMethod("CreateMenu", flags),
                il => PatchRefresh(il, searchEngine));
            chapterPickerEntry = new ILHook(entry, il => PatchRefresh(il,
                typeof(EverestChapterLevelSetPickerSequence).GetField("engine", flags)));
            var picker = typeof(EverestFileSelectSlotState).Assembly.GetType(
                "Celeste.Everest.UI.Menus.Files.EverestFileSelectSlotStateLevelSetPicker", true);
            filePicker = new ILHook(picker.GetMethod("changeStartingLevelSet", flags),
                il => PatchFilePicker(il, picker.GetField("engine", flags)));
            LoadTasCampaignHooks();
        }

        private void LoadTasCampaignHooks()
        {
            if (((ISourceRuntimeExtensionEnvironment)engine).GetRuntimeExtension<TasRuntimeEnvironment>() == null)
                return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var command = typeof(TasRuntimeEnvironment).Assembly.GetType(
                "Celeste.Mods.Tas.Commands.SourceTasSelectCampaignProcess", true);
            var driver = command.GetField("driver", flags);
            var process = driver.FieldType.GetProperty("Process", flags).GetGetMethod(true);
            Action<ILCursor> emitCommandOwner = cursor =>
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, driver);
                cursor.Emit(OpCodes.Callvirt, process);
            };
            tasCampaignSelection = new ILHook(command.GetMethod("ChangeSelectedCampaign", flags),
                il => PatchTasCampaignClassification(il, emitCommandOwner, 2, false));
            tasCampaignValidation = new ILHook(command.GetMethod("SelectCampaign", flags),
                il => PatchTasCampaignClassification(il, emitCommandOwner, 1, true));
            var autocomplete = command.GetNestedType("Meta", BindingFlags.NonPublic)
                .GetMethod("GetAutoCompleteEntries");
            var iterator = autocomplete.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            var owner = command.GetProperty("Owner", BindingFlags.Static | BindingFlags.NonPublic).GetGetMethod(true);
            tasCampaignCompletions = new ILHook(iterator.GetMethod("MoveNext", flags), il =>
                PatchTasCampaignClassification(il, cursor =>
                {
                    cursor.Emit(OpCodes.Call, owner);
                    cursor.Emit(OpCodes.Ldfld, driver);
                    cursor.Emit(OpCodes.Callvirt, process);
                }, 1, false));
        }

        private void PatchTasCampaignClassification(ILContext il, Action<ILCursor> emitOwner, int expectedCalls,
            bool refresh)
        {
            var cursor = new ILCursor(il);
            var levelSet = new VariableDefinition(il.Import(typeof(string)));
            il.Body.Variables.Add(levelSet);
            int calls = 0;
            while (cursor.TryGotoNext(MoveType.Before,
                       instruction => instruction.MatchCallvirt<Func<string, bool>>("Invoke")))
            {
                cursor.Emit(OpCodes.Dup);
                cursor.Emit(OpCodes.Stloc, levelSet);
                if (refresh)
                {
                    emitOwner(cursor);
                    cursor.EmitDelegate<Action<ISourceEngineContext>>(RefreshHelperTests);
                }

                cursor.Index++;
                cursor.Emit(OpCodes.Ldloc, levelSet);
                emitOwner(cursor);
                cursor.EmitDelegate<Func<bool, string, ISourceEngineContext, bool>>(HideTasCampaign);
                calls++;
            }

            if (calls != expectedCalls)
                throw new InvalidOperationException("The actual TAS campaign classification calls differ: " +
                                                    il.Method.FullName + " (" + calls + "/" + expectedCalls + ").");
        }

        private bool HideTasCampaign(bool collab, string levelSet, ISourceEngineContext owner)
            => collab || ReferenceEquals(owner, engine) && helperTests.IsHiddenLevelSet(levelSet);

        private void PatchFilePicker(ILContext il, FieldInfo engineField)
        {
            PatchRefresh(il, engineField);
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before, instruction =>
                instruction.MatchCallvirt<System.Collections.Generic.IReadOnlyList<IOriginalAreaData>>("get_Item"));
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, engineField);
            cursor.EmitDelegate<Func<int, int, ISourceEngineContext, int>>(SkipHiddenFileLevelSets);
        }

        private int SkipHiddenFileLevelSets(int id, int direction, ISourceEngineContext owner)
        {
            if (!ReferenceEquals(owner, engine)) return id;
            var areas = ((IOriginalSessionConstructionProcess)engine).AreaTable.Areas;
            string currentLevelSet = areas[id].LevelSet;
            while (helperTests.IsHiddenLevelSet(currentLevelSet) || identities.IsCollabLevelSet(currentLevelSet))
            {
                if (direction <= 0)
                {
                    for (int i = 0; i < areas.Count; i++)
                        if (areas[i].LevelSet == currentLevelSet)
                        {
                            id = i + direction;
                            break;
                        }
                }
                else
                {
                    for (int i = areas.Count - 1; i >= 0; i--)
                        if (areas[i].LevelSet == currentLevelSet)
                        {
                            id = i + direction;
                            break;
                        }
                }

                if (id >= areas.Count) id = 0;
                if (id < 0) id = areas.Count - 1;
                currentLevelSet = areas[id].LevelSet;
            }

            return id;
        }

        private void PatchRefresh(ILContext il, FieldInfo engineField)
        {
            var cursor = new ILCursor(il);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, engineField);
            cursor.EmitDelegate<Action<ISourceEngineContext>>(RefreshHelperTests);
        }

        private void RefreshHelperTests(ISourceEngineContext owner)
        {
            if (ReferenceEquals(owner, engine))
                helperTests = new HelperTestMapVisibility(
                    ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry);
        }

        private void PatchChapterPicker(ILContext il, FieldInfo ownerField)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                instruction => instruction.MatchCallvirt<IOriginalAreaData>("get_LevelSet"));
            var areaLocal = new VariableDefinition(il.Import(typeof(IOriginalAreaData)));
            il.Body.Variables.Add(areaLocal);
            cursor.Emit(OpCodes.Dup);
            cursor.Emit(OpCodes.Stloc, areaLocal);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<string>("op_Inequality"));
            cursor.Emit(OpCodes.Ldloc, areaLocal);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, ownerField);
            cursor.Emit(OpCodes.Ldfld, typeof(EverestChapterLevelSetPickerSequence)
                .GetField("engine", BindingFlags.Instance | BindingFlags.NonPublic));
            cursor.EmitDelegate<Func<bool, IOriginalAreaData, ISourceEngineContext, bool>>(HasVisibleChapterSet);
        }

        private bool HasVisibleChapterSet(bool original, IOriginalAreaData area, ISourceEngineContext owner)
            => original && (!ReferenceEquals(owner, engine) ||
                            (!helperTests.IsHiddenMap(area.SID) && !identities.IsCollabLevelSet(area.LevelSet)));

        private void PatchMapItems(ILContext il, FieldInfo engineField)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                instruction => instruction.MatchCallvirt<OriginalAreaData>(nameof(EverestArea.HasMode)) ||
                               instruction.MatchCall<OriginalAreaData>(nameof(EverestArea.HasMode)));
            var areaLocal = new VariableDefinition(il.Import(typeof(EverestArea)));
            var modeLocal = new VariableDefinition(il.Import(typeof(int)));
            il.Body.Variables.Add(areaLocal);
            il.Body.Variables.Add(modeLocal);
            cursor.Emit(OpCodes.Stloc, modeLocal);
            cursor.Emit(OpCodes.Dup);
            cursor.Emit(OpCodes.Stloc, areaLocal);
            cursor.Emit(OpCodes.Ldloc, modeLocal);
            cursor.Index++;
            cursor.Emit(OpCodes.Ldloc, areaLocal);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, engineField);
            cursor.EmitDelegate<Func<bool, EverestArea, ISourceEngineContext, bool>>(HasVisibleMode);
        }

        private bool HasVisibleMode(bool original, EverestArea area, ISourceEngineContext owner)
            => original && (!ReferenceEquals(owner, engine) ||
                            (!helperTests.IsHiddenMap(area.SID) && !identities.IsCollabLevelSet(area.LevelSet)));

        private void PatchLevelSets(ILContext il)
        {
            PatchRefresh(il, listEngine);
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before, instruction => instruction.MatchLdstr("Celeste"),
                instruction => instruction.MatchCall<string>("op_Equality"));
            cursor.Emit(OpCodes.Dup);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<string>("op_Equality"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, listEngine);
            cursor.EmitDelegate<Func<string, bool, ISourceEngineContext, bool>>(HideLevelSet);
        }

        private bool HideLevelSet(string levelSet, bool original, ISourceEngineContext owner)
            => original || ReferenceEquals(owner, engine) &&
                (helperTests.IsHiddenLevelSet(levelSet) || identities.IsCollabLevelSet(levelSet));

        public void Dispose()
        {
            tasCampaignCompletions?.Dispose();
            tasCampaignCompletions = null;
            tasCampaignValidation?.Dispose();
            tasCampaignValidation = null;
            tasCampaignSelection?.Dispose();
            tasCampaignSelection = null;
            filePicker?.Dispose();
            filePicker = null;
            chapterPickerEntry?.Dispose();
            chapterPickerEntry = null;
            searchMenu?.Dispose();
            searchMenu = null;
            chapterPicker?.Dispose();
            chapterPicker = null;
            searchItems?.Dispose();
            searchItems = null;
            listMenu?.Dispose();
            listMenu = null;
            listItems?.Dispose();
            listItems = null;
        }
    }
}
