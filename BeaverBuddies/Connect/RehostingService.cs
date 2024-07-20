using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Timberborn.Autosaving;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.GameSaveRuntimeSystemUI;
using Timberborn.InputSystem;
using Timberborn.SaveSystem;
using Timberborn.SettlementNameSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using static Timberborn.GameSaveRuntimeSystem.GameSaver;

namespace BeaverBuddies.Connect
{
    public class RehostingService
    {
        private readonly AutosaveNameService _autosaveNameService;
        private readonly GameSaver _gameSaver;
        private readonly GameSaveRepository _gameSaveRepository;
        private readonly SettlementNameService _settlementNameService;
        private readonly ValidatingGameLoader _validatingGameLoader;


        public RehostingService(
            AutosaveNameService autosaveNameService, 
            GameSaver gameSaver, 
            GameSaveRepository gameSaveRepository, 
            SettlementNameService settlementNameService,
            ValidatingGameLoader validatingGameLoader
        ) 
        {
            _autosaveNameService = autosaveNameService;
            _gameSaver = gameSaver;
            _gameSaveRepository = gameSaveRepository;
            _settlementNameService = settlementNameService;
            _validatingGameLoader = validatingGameLoader;
        }

        // TODO: Should probably check IEnumerable<IAutosaveBlocker> autosaveBlockers
        // that Autosaver uses, both here and in general when a client joins to avoid
        // saving when it could corrupt things. Hopefully the save would fail if
        // there's a real issue, rather than corrupting, but I don't know...
        public bool RehostGame()
        {
            string settlementName = _settlementNameService.SettlementName;
            string saveName = _autosaveNameService.Timestamp().Replace(",", "") + " Rehost";
            SaveReference saveReference = new SaveReference(settlementName, saveName);
            try
            {
                _gameSaver.InstantSaveSkippingNameValidation(saveReference, () => 
                {
                    // Run on next frame because the GameSaver doesn't release its
                    // handle on the save stream until the method finishes executing
                    // i.e. after the callback has run.
                    _gameSaver.StartCoroutine(RunOnNextFrameCoroutine(() =>
                    {
                        ServerHostingUI.LoadAndHost(_validatingGameLoader, saveReference);
                    }));
                });
            }
            catch (GameSaverException ex)
            {
                Plugin.LogError($"Error occured while saving: {ex.InnerException}");
                _gameSaveRepository.DeleteSaveSafely(saveReference);
                return false;
            } catch (Exception ex)
            {
                Plugin.LogError($"Failed to rehost: {ex}");
                return false;
            }
            return true;
        }

        private IEnumerator RunOnNextFrameCoroutine(Action action)
        {
            yield return null;
            action?.Invoke();
        }
    }
}
