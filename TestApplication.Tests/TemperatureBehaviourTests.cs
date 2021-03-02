using Common;
using DbDataAccess;
using LightDriverService;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhino.Mocks;
using System;
using System.Threading.Tasks;
using TestApplication.Repositories;

namespace TestApplication.Tests
{
    [TestClass]
    public class TemperatureBehaviourTests
    {
        Guid _bulbId = Guid.NewGuid();
        IDataService _testService;
        ILightDriverService _lightDriver;
        IStreetlightRepository _repository;

        [TestInitialize]
        public void SetupTests()
        {
            _lightDriver = MockRepository.GenerateMock<ILightDriverService>();
            _lightDriver.Stub(x => x.SwitchOffBulb(Arg<Guid>.Is.Anything)).Return(GetBooleanTask(true));
            _lightDriver.Stub(x => x.SwitchOnBulb(Arg<Guid>.Is.Anything)).Return(GetBooleanTask(true));
            _testService = MockRepository.GenerateMock<IDataService>();

            _repository = new StreetlightRepository(_testService
                , _lightDriver);
        }

        private Task<bool> GetBooleanTask(bool value)
        {
            var task = new Task<bool>(() =>
            {
                return value;
            });
            task.Start();
            return task;
        }

        [TestMethod]
        public async Task StreetlightRepository_LoggingTemperature_GetsAndUpdates()
        {
            _testService.Expect(x => x.GetBulbState(_bulbId))
                .Return(GetTask(new BulbState()
                {
                    BulbCurrentState = new BulbData()
                    {
                        IsOn = false
                    },
                    BulbInformation = new BulbModel()
                    {
                        MaxTemperature = 100
                    }
                }));
            _testService.Expect(x => x.UpdateBulbStatus(Arg<BulbState>.Is.Anything
                , Arg<bool>.Is.Anything
                , Arg<FaultCode>.Is.Anything, Arg<BulbData>.Is.Anything))
                .Return(GetBooleanTask(true));
            await _repository.SetBulbTemperature(_bulbId, 20);

            _testService.VerifyAllExpectations();
        }

        private Task<BulbState> GetTask(BulbState bulbState)
        {
            var task = new Task<BulbState>(() =>
            {
                return bulbState;
            });
            task.Start();
            return task;
        }

        [TestMethod]
        public async Task StreetlightRepository_LoggingTemperatureWithBulbOff_DoesNotContactLightService()
        {
            _testService.Stub(x => x.GetBulbState(_bulbId))
                .Return(GetTask(new BulbState()
                {
                    BulbCurrentState = new BulbData()
                    {
                        IsOn = false
                    },
                    BulbInformation = new BulbModel()
                    {
                        MaxTemperature = 100
                    }
                }));
            _testService.Expect(x => x.UpdateBulbStatus(Arg<BulbState>.Is.Anything
                , Arg<bool>.Is.Anything
                , Arg<FaultCode>.Is.Anything, Arg<BulbData>.Is.Anything))
                .Return(GetBooleanTask(true));
            await _repository.SetBulbTemperature(_bulbId, 20);

            _lightDriver.AssertWasNotCalled(x => x.SwitchOnBulb(_bulbId));
            _testService.VerifyAllExpectations();
        }

        [TestMethod]
        public async Task StreetlightRepository_LoggingTemperatureBulbOnOverTemp_SwitchesOffBulb()
        {
            _lightDriver.Stub(x => x.SwitchOffBulb(_bulbId))
                .Return(GetBooleanTask(true));
            _testService.Stub(x => x.GetBulbState(_bulbId))
                .Return(GetTask(new BulbState()
                {
                    BulbCurrentState = new BulbData()
                    {
                        IsOn = true
                    },
                    BulbInformation = new BulbModel()
                    {
                        MaxTemperature = 100
                    }
                }));

            _testService.Stub(x => x.UpdateBulbStatus(Arg<BulbState>.Is.Anything
                , Arg<bool>.Is.Anything
                , Arg<FaultCode>.Is.Anything, Arg<BulbData>.Is.Anything))
                .Return(GetBooleanTask(true));

            await _repository.SetBulbTemperature(_bulbId, 200);

            _lightDriver.AssertWasCalled(x => x.SwitchOffBulb(_bulbId));
            _testService.VerifyAllExpectations();
        }

        [TestMethod]
        public async Task StreetlightRepository_BulbOverTemp_WillNotSwitchOn()
        {

            _testService.Stub(x => x.GetBulbState(_bulbId))
                .Return(GetTask(new BulbState()
                {
                    BulbCurrentState = new BulbData()
                    {
                        IsOn = true,
                        BulbTemperature = 200
                    },
                    BulbInformation = new BulbModel()
                    {
                        MaxTemperature = 100
                    }
                }));
            await _repository.SwitchOnBulb(_bulbId);

            _lightDriver.AssertWasNotCalled(x => x.SwitchOnBulb(_bulbId));
            _testService.VerifyAllExpectations();
        }

        [TestMethod]
        public async Task StreetlightRepository_BulbWithNoMaxTemperature_WillSwitchOn()
        {

            _lightDriver.Expect(x => x.SwitchOnBulb(_bulbId)).Return(GetBooleanTask(true));

            _testService.Stub(x => x.GetBulbState(_bulbId))
                .Return(GetTask(new BulbState()
                {
                    BulbCurrentState = new BulbData()
                    {
                        IsOn = true,
                        BulbTemperature = 0
                    },
                    BulbInformation = new BulbModel()
                    {
                        MaxTemperature = 0
                    }
                }));

            _testService.Stub(x => x.UpdateBulbStatus(Arg<BulbState>.Is.Anything
                , Arg<bool>.Is.Anything
                , Arg<FaultCode>.Is.Anything, Arg<BulbData>.Is.Anything))
                .Return(GetBooleanTask(true));

            await _repository.SwitchOnBulb(_bulbId);

            _lightDriver.AssertWasCalled(x => x.SwitchOnBulb(_bulbId));
            _testService.VerifyAllExpectations();
        }
    }
}
