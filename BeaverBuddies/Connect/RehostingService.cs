using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Autosaving;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRepositorySystemUI;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.GameSaveRuntimeSystemUI;
using Timberborn.SettlementNameSystem;
using Timberborn.SingletonSystem;

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
                _gameSaver.InstantSaveSkippingNameValidation(saveReference, () => { });
                ServerHostingUI.LoadAndHost(_validatingGameLoader, saveReference);
            }
            catch (GameSaverException ex)
            {
                Plugin.LogError($"Error occured while saving: {ex.InnerException}");
                _gameSaveRepository.DeleteSaveSafely(saveReference);
                return false;
            }
            return true;
        }
    }
}
