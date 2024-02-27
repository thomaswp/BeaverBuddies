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
        private readonly SaveTimestampFormatter _saveTimestampFormatter;
        private readonly GameSaver _gameSaver;
        private readonly GameSaveRepository _gameSaveRepository;
        private readonly SettlementNameService _settlementNameService;
        private readonly ValidatingGameLoader _validatingGameLoader;


        public RehostingService(
            SaveTimestampFormatter saveTimestampFormatter, 
            GameSaver gameSaver, 
            GameSaveRepository gameSaveRepository, 
            SettlementNameService settlementNameService,
            ValidatingGameLoader validatingGameLoader
        ) 
        { 
            _saveTimestampFormatter = saveTimestampFormatter;
            _gameSaver = gameSaver;
            _gameSaveRepository = gameSaveRepository;
            _settlementNameService = settlementNameService;
            _validatingGameLoader = validatingGameLoader;
        }

        public bool RehostGame()
        {
            string settlementName = _settlementNameService.SettlementName;
            string saveName = _saveTimestampFormatter.Timestamp() + " Rehost";
            SaveReference saveReference = new SaveReference(settlementName, saveName);
            try
            {
                _gameSaver.InstantSaveSkippingNameValidation(saveReference, () =>
                {
                    ServerHostingUI.LoadAndHost(_validatingGameLoader, saveReference);
                });
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
