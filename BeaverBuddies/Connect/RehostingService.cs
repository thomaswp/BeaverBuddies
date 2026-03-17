using BeaverBuddies.Util;
using System;
using Timberborn.Autosaving;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.SettlementNameSystem;

namespace BeaverBuddies.Connect
{
    public class RehostingService
    {
        private readonly AutosaveNameService _autosaveNameService;
        private readonly GameSaver _gameSaver;
        private readonly GameSaveRepository _gameSaveRepository;
        private readonly SettlementReferenceService _settlementReferenceService;
        private readonly ValidatingGameLoader _validatingGameLoader;
        private readonly DialogBoxShower _dialogBoxShower;


        public RehostingService(
            AutosaveNameService autosaveNameService, 
            GameSaver gameSaver, 
            GameSaveRepository gameSaveRepository,
            SettlementReferenceService settlementReferenceService,
            ValidatingGameLoader validatingGameLoader,
            DialogBoxShower dialogBoxShower
        ) 
        {
            _autosaveNameService = autosaveNameService;
            _gameSaver = gameSaver;
            _gameSaveRepository = gameSaveRepository;
            _settlementReferenceService = settlementReferenceService;
            _validatingGameLoader = validatingGameLoader;
            _dialogBoxShower = dialogBoxShower;
        }

        // TODO: Should probably check IEnumerable<IAutosaveBlocker> autosaveBlockers
        // that Autosaver uses, both here and in general when a client joins to avoid
        // saving when it could corrupt things. Hopefully the save would fail if
        // there's a real issue, rather than corrupting, but I don't know...
        public bool SaveRehostFile(Action<SaveReference> callback, bool waitUntilAccessible)
        {
            if (waitUntilAccessible)
            {
                Action<SaveReference> originalCallback = callback;
                callback = saveReference =>
                {
                    // Run on next frame because the GameSaver doesn't release its
                    // handle on the save stream until the method finishes executing
                    // i.e. after the callback has run.
                    var mono = ServerHostingUtils.GetMonoBehaviour(_settlementReferenceService._sceneLoader);
                    TimeoutUtils.RunAfterFrames(mono, () =>
                    {
                        originalCallback(saveReference);
                    });
                };
            }
            SettlementReference settlementReference = _settlementReferenceService.SettlementReference;
            string saveName = _autosaveNameService.Timestamp().Replace(",", "") + " Rehost";
            SaveReference saveReference = new SaveReference(saveName, settlementReference);
            try
            {
                _gameSaver.SaveInstantlySkippingNameValidation(saveReference, () =>
                {
                    callback(saveReference);
                });
            }
            catch (GameSaverException ex)
            {
                Plugin.LogError($"Error occured while saving: {ex.InnerException}");
                _gameSaveRepository.DeleteSaveSafely(saveReference);
                return false;
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Failed to rehost: {ex}");
                return false;
            }
            return true;
        }

        public bool RehostGame()
        {
            return SaveRehostFile(LoadGame, true);
        }

        public void LoadGame(SaveReference saveReference)
        {
            ServerHostingUtils.LoadIfSaveValidAndHost(_validatingGameLoader, _dialogBoxShower, saveReference);
        }
    }
}
