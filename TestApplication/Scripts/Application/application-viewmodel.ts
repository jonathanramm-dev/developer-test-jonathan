/// <reference path="../Definitions/knockout.d.ts" />
/// <reference path="../Definitions/jquery.d.ts" />

module Rsl {
    export class ApplicationViewModel {
        public streetlights: KnockoutObservable<Models.IStreetlightSummary[]>;
        public selectedStreetlight: KnockoutObservable<IStreetlightDetailViewModel>;
        public totalPowerDrawStreetlight: KnockoutComputed<number>
        public allBulbsWhatColorForIndicatorCircle: KnockoutComputed<string[]>
        public justReturnRed: KnockoutObservable<string>;
        public turnOnStreetLightBtnText: KnockoutComputed<string>;
        public isStreetlightDataLoading: KnockoutObservable<boolean>;

        // get applicant to add a loader here
        constructor(private _apiAccess: IApiAccess) {
            this.streetlights = ko.observable<Models.IStreetlightSummary[]>();
            this.selectedStreetlight = ko.observable<IStreetlightDetailViewModel>();
            this.isStreetlightDataLoading = ko.observable<boolean>(false);

            this.turnOnStreetLightBtnText = ko.computed<string>(() => {
                try {
                    if (this.isStreetlightDataLoading() ) {
                        return "Loading..."
                    } else if (this.selectedStreetlight().isSwitchedOn()) {
                        return "Switch Off"
                    } else {
                        return "Switch On"
                    }
                } catch (err) {
                    //console.log(err);
                }
            })

            this.totalPowerDrawStreetlight = ko.computed<number>(() => {
                try {
                    let totalPowerForBulbs = this.selectedStreetlight().bulbs.reduce((total, arrayItem) => {
                        return total + (arrayItem.bulbStatus().isOn ? arrayItem.bulbInformation.powerDraw : 0);
                    }, 0);
                    // let self = this;
                    let powerForStreetLight = this.selectedStreetlight().isSwitchedOn() ? this.selectedStreetlight().electricalDraw : 0;
                    return totalPowerForBulbs + powerForStreetLight
                }
                catch (err) {
                    return 0;
                }
            })

            this.justReturnRed = ko.observable("red")

            this.allBulbsWhatColorForIndicatorCircle = ko.computed<string[]>(() => {
                try {
                    let backgroundColor = this.selectedStreetlight().bulbs.map((bulbItem) => {
                        let bulbCurrentTemp = bulbItem.bulbStatus().bulbTemperature
                        let bulbMaxTemp = bulbItem.bulbInformation.maxTemperature
                        let bulbAboveMaxTemp = bulbCurrentTemp > bulbMaxTemp
                        // todo: integrate if there is a fault
                        let bulbOn = bulbItem.bulbStatus().isOn

                        if (bulbAboveMaxTemp) { return 'red' }
                        if (bulbOn) { return 'yellow' }
                        else { return 'grey' }
                    });
                    
                    return backgroundColor
                } catch (err) {
                    console.log("bulb error");
                    return ['grey','grey','grey','grey','grey']
                }
            })

            this.loadData().done(x => {
                this.streetlights(x);
            });
        }

        public selectStreetlight(parent: ApplicationViewModel, streetlight: Models.IStreetlightSummary): void {
            parent.selectedStreetlight(null);
            parent.loadDetails(streetlight.id).done(x => {
                parent.selectedStreetlight(x);
            });
        }

        public clickObject(parent:any, data: any): void {
            parent.set(data);
        }
        public isFailed(bulb: IBulbStateViewModel): boolean {
            return bulb.bulbStatus().fault > 0;
        }

        public toggleLightState(light: IStreetlightDetailViewModel): void {
            console.log("toggleLightState");
            var isOn = light.isSwitchedOn();

          //  this.isStreetlightDataLoading(true)
            if (isOn) {
                this._apiAccess.switchOffLight(light.id).always(x => {
                    this.selectStreetlight(this, {
                        id: light.id,
                        description: light.description
                    });
                    //this.isStreetlightDataLoading(false)
                });
            }
            else {
                this._apiAccess.switchOnLight(light.id).always(x => {
                    this.selectStreetlight(this, {
                        id: light.id,
                        description: light.description
                    });
                   // this.isStreetlightDataLoading(false)
                });
            }
        }

        public toggleBulbState(parent: ApplicationViewModel, bulb: IBulbStateViewModel): void {
            var isBulbOn = bulb.bulbStatus().isOn;

            if (isBulbOn === true) {
                // always switch off
                parent._apiAccess.switchOffBulb(bulb.bulbInformation.id).done(x => {
                    console.log("toggle off");
                    parent.updateBulbStatus(bulb);
                });
            }
            if (isBulbOn === false) {
                let streetLightOn = parent.selectedStreetlight().isSwitchedOn(); // is the streetlight on or off
                if (streetLightOn === true) {
                    parent._apiAccess.switchOnBulb(bulb.bulbInformation.id).done(x => {
                        console.log("toggle on");
                        parent.updateBulbStatus(bulb);
                    });
                }
                if (streetLightOn === false) {
                    // if the streetlight is off then don't switch on
                }
            }
        }

        private updateBulbStatus(bulb: IBulbStateViewModel) {
            this._apiAccess.loadBulbDetail(bulb.bulbInformation.id).done(x => {
                bulb.bulbStatus(x.bulbCurrentState);
            });
        }

        private loadData(): JQueryPromise<Models.IStreetlightSummary[]> {
            return this._apiAccess.loadStreetlights();
        }

        private loadDetails(id: string): JQueryPromise<IStreetlightDetailViewModel> {
            return this._apiAccess.loadStreetlightDetail(id);
        }
    }
}