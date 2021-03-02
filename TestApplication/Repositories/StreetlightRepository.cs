using DbDataAccess;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LightDriverService;
using Common;

namespace TestApplication.Repositories
{
    public class StreetlightRepository : IStreetlightRepository
    {
        private readonly IDataService _dataService;
        private readonly ILightDriverService _lightDriver;

        public StreetlightRepository(IDataService dataService
            , ILightDriverService lightDriver)
        {
            _dataService = dataService;
            _lightDriver = lightDriver;
        }

        public Task<IEnumerable<StreetlightSummaryModel>> GetAllStreetlights()
        {
            return _dataService.GetStreetlightListing();
        }

        public Task<StreetlightModel> GetStreetlight(Guid lightId)
        {
            return _dataService.GetStreetlightDetail(lightId);
        }

        public Task<bool> RegisterBulbFailure(Guid bulbId)
        {
            return _dataService.UpdateBulbStatus(bulbId, false, Common.FaultCode.GeneralFailure);
        }

        public Task<bool> RegisterFault(Guid bulbId, FaultCode fault)
        {
            return _dataService.UpdateBulbStatus(bulbId, false, fault);
        }

        public Task<bool> RegisterBulbOff(Guid bulbId)
        {

            return _dataService.UpdateBulbStatus(bulbId, false);
        }

        public Task<bool> RegisterBulbOn(Guid bulbId)
        {
            return _dataService.UpdateBulbStatus(bulbId, true);
        }

        public Task<Guid> ReplaceBulb(Guid originalBulbId)
        {
            return _dataService.ReplaceBulb(originalBulbId);
        }

        // simulation
        public async Task<bool> SetBulbTemperature(Guid bulbId, double temperature)
        {
            // get bulb
            BulbState bulb = await _dataService.GetBulbState(bulbId);
            bulb.BulbCurrentState.BulbTemperature = temperature;
            if (bulb.BulbCurrentState.IsOn)
            {
                // check over temperature
                if (!CanSwitchBulbOn(bulb))
                {
                    // fault condition!
                    await Task.WhenAll(_lightDriver.SwitchOffBulb(bulbId)
                        , _dataService.UpdateBulbStatus(bulb, false, FaultCode.OverTemperature, bulb.BulbCurrentState));
                    return false;
                }
            }
            
            // the bulb is off or not over temperature - no worries - log the data
            bulb.BulbCurrentState.BulbTemperature = temperature;

            await _dataService.UpdateBulbStatus(bulb, false, null, bulb.BulbCurrentState); // fire-forget
            return false;
        }

        public async Task<bool> SwitchOffBulb(Guid bulbId)
        {
            await Task.WhenAll(_lightDriver.SwitchOffBulb(bulbId)
                , _dataService.UpdateBulbStatus(bulbId, false));

            return true;
        }

        public async Task<BulbState> GetBulbState(Guid bulbId)
        {
            return await _dataService.GetBulbState(bulbId);
        }

        public async Task<bool> SwitchOnBulb(Guid bulbId)
        {
            // check bulb temperature before attempting switch on
            var bulbData = await _dataService.GetBulbState(bulbId);

            if (!CanSwitchBulbOn(bulbData))
            {
                return false; // do not switch it on!
            }
            else // switch on as instructed
            {
                await Task.WhenAll(_lightDriver.SwitchOnBulb(bulbId)
                    , _dataService.UpdateBulbStatus(bulbData, true));
            }
            return true;
        }

        private bool CanSwitchBulbOn(BulbState bulbState)
        {
            return true;
        }

        public async Task<bool> SwitchOffLight(Guid lightId)
        {
            var success = await _lightDriver.SwitchOffLight(lightId);
            var streetlightDetail = await _dataService.GetStreetlightDetail(lightId); // kills power to the light

            foreach (var bulb in streetlightDetail.Bulbs)
            {
                await _dataService.UpdateBulbStatus(bulb.BulbInformation.Id, false); // shut off all bulbs without mercy
            }

            await _dataService.UpdateStreetlightStatus(lightId, false);

            return true;
        }

        public async Task<IEnumerable<Guid>> SwitchOnLight(Guid lightId)
        {
            // we switch on only bulbs that are valid
            var streetlightDetail = await _dataService.GetStreetlightDetail(lightId);
            IList<Guid> bulbsSwitchedOn = new List<Guid>();

            foreach (var bulb in streetlightDetail.Bulbs)
            {
                if (CanSwitchBulbOn(bulb) && await _lightDriver.SwitchOnBulb(bulb.BulbInformation.Id))
                {
                    lock (bulbsSwitchedOn)
                    {
                        bulbsSwitchedOn.Add(bulb.BulbInformation.Id);
                    }

                    await _dataService.UpdateBulbStatus(bulb, true);
                }
            }

            await _dataService.UpdateStreetlightStatus(lightId, true);

            return bulbsSwitchedOn;
        }

        public Task<bool> SetAmbientLightLevel(int lumens)
        {
            throw new NotImplementedException();
        }
    }
}