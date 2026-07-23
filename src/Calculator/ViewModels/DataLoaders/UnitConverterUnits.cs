// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp
{
    namespace ViewModel.Common
    {

        public readonly record struct UnitConverterUnits
        {
            private UnitConverterUnits(int value)
            {
                Value = value;
            }

            public int Value { get; }

            public int ToInt32()
            {
                return Value;
            }

            public static explicit operator int(UnitConverterUnits unit)
            {
                return unit.ToInt32();
            }

            public static UnitConverterUnits UnitStart { get; } = new(0);

            public static UnitConverterUnits AreaAcre { get; } = new(1);

            public static UnitConverterUnits AreaHectare { get; } = new(2);

            public static UnitConverterUnits AreaSquareCentimeter { get; } = new(3);

            public static UnitConverterUnits AreaSquareFoot { get; } = new(4);

            public static UnitConverterUnits AreaSquareInch { get; } = new(5);

            public static UnitConverterUnits AreaSquareKilometer { get; } = new(6);

            public static UnitConverterUnits AreaSquareMeter { get; } = new(7);

            public static UnitConverterUnits AreaSquareMile { get; } = new(8);

            public static UnitConverterUnits AreaSquareMillimeter { get; } = new(9);

            public static UnitConverterUnits AreaSquareYard { get; } = new(10);

            public static UnitConverterUnits DataBit { get; } = new(11);

            public static UnitConverterUnits DataByte { get; } = new(12);

            public static UnitConverterUnits DataGigabit { get; } = new(13);

            public static UnitConverterUnits DataGigabyte { get; } = new(14);

            public static UnitConverterUnits DataKilobit { get; } = new(15);

            public static UnitConverterUnits DataKilobyte { get; } = new(16);

            public static UnitConverterUnits DataMegabit { get; } = new(17);

            public static UnitConverterUnits DataMegabyte { get; } = new(18);

            public static UnitConverterUnits DataPetabit { get; } = new(19);

            public static UnitConverterUnits DataPetabyte { get; } = new(20);

            public static UnitConverterUnits DataTerabit { get; } = new(21);

            public static UnitConverterUnits DataTerabyte { get; } = new(22);

            public static UnitConverterUnits EnergyBritishThermalUnit { get; } = new(23);

            public static UnitConverterUnits EnergyCalorie { get; } = new(24);

            public static UnitConverterUnits EnergyElectronVolt { get; } = new(25);

            public static UnitConverterUnits EnergyFootPound { get; } = new(26);

            public static UnitConverterUnits EnergyJoule { get; } = new(27);

            public static UnitConverterUnits EnergyKilocalorie { get; } = new(28);

            public static UnitConverterUnits EnergyKilojoule { get; } = new(29);

            public static UnitConverterUnits LengthCentimeter { get; } = new(30);

            public static UnitConverterUnits LengthFoot { get; } = new(31);

            public static UnitConverterUnits LengthInch { get; } = new(32);

            public static UnitConverterUnits LengthKilometer { get; } = new(33);

            public static UnitConverterUnits LengthMeter { get; } = new(34);

            public static UnitConverterUnits LengthMicron { get; } = new(35);

            public static UnitConverterUnits LengthMile { get; } = new(36);

            public static UnitConverterUnits LengthMillimeter { get; } = new(37);

            public static UnitConverterUnits LengthNanometer { get; } = new(38);

            public static UnitConverterUnits LengthNauticalMile { get; } = new(39);

            public static UnitConverterUnits LengthYard { get; } = new(40);

            public static UnitConverterUnits PowerBritishThermalUnitPerMinute { get; } = new(41);

            public static UnitConverterUnits PowerFootPoundPerMinute { get; } = new(42);

            public static UnitConverterUnits PowerHorsepower { get; } = new(43);

            public static UnitConverterUnits PowerKilowatt { get; } = new(44);

            public static UnitConverterUnits PowerWatt { get; } = new(45);

            public static UnitConverterUnits TemperatureDegreesCelsius { get; } = new(46);

            public static UnitConverterUnits TemperatureDegreesFahrenheit { get; } = new(47);

            public static UnitConverterUnits TemperatureKelvin { get; } = new(48);

            public static UnitConverterUnits TimeDay { get; } = new(49);

            public static UnitConverterUnits TimeHour { get; } = new(50);

            public static UnitConverterUnits TimeMicrosecond { get; } = new(51);

            public static UnitConverterUnits TimeMillisecond { get; } = new(52);

            public static UnitConverterUnits TimeMinute { get; } = new(53);

            public static UnitConverterUnits TimeSecond { get; } = new(54);

            public static UnitConverterUnits TimeWeek { get; } = new(55);

            public static UnitConverterUnits TimeYear { get; } = new(56);

            public static UnitConverterUnits SpeedCentimetersPerSecond { get; } = new(57);

            public static UnitConverterUnits SpeedFeetPerSecond { get; } = new(58);

            public static UnitConverterUnits SpeedKilometersPerHour { get; } = new(59);

            public static UnitConverterUnits SpeedKnot { get; } = new(60);

            public static UnitConverterUnits SpeedMach { get; } = new(61);

            public static UnitConverterUnits SpeedMetersPerSecond { get; } = new(62);

            public static UnitConverterUnits SpeedMilesPerHour { get; } = new(63);

            public static UnitConverterUnits VolumeCubicCentimeter { get; } = new(64);

            public static UnitConverterUnits VolumeCubicFoot { get; } = new(65);

            public static UnitConverterUnits VolumeCubicInch { get; } = new(66);

            public static UnitConverterUnits VolumeCubicMeter { get; } = new(67);

            public static UnitConverterUnits VolumeCubicYard { get; } = new(68);

            public static UnitConverterUnits VolumeCupUS { get; } = new(69);

            public static UnitConverterUnits VolumeFluidOunceUK { get; } = new(70);

            public static UnitConverterUnits VolumeFluidOunceUS { get; } = new(71);

            public static UnitConverterUnits VolumeGallonUK { get; } = new(72);

            public static UnitConverterUnits VolumeGallonUS { get; } = new(73);

            public static UnitConverterUnits VolumeLiter { get; } = new(74);

            public static UnitConverterUnits VolumeMilliliter { get; } = new(75);

            public static UnitConverterUnits VolumePintUK { get; } = new(76);

            public static UnitConverterUnits VolumePintUS { get; } = new(77);

            public static UnitConverterUnits VolumeTablespoonUS { get; } = new(78);

            public static UnitConverterUnits VolumeTeaspoonUS { get; } = new(79);

            public static UnitConverterUnits VolumeQuartUK { get; } = new(80);

            public static UnitConverterUnits VolumeQuartUS { get; } = new(81);

            public static UnitConverterUnits WeightCarat { get; } = new(82);

            public static UnitConverterUnits WeightCentigram { get; } = new(83);

            public static UnitConverterUnits WeightDecigram { get; } = new(84);

            public static UnitConverterUnits WeightDecagram { get; } = new(85);

            public static UnitConverterUnits WeightGram { get; } = new(86);

            public static UnitConverterUnits WeightHectogram { get; } = new(87);

            public static UnitConverterUnits WeightKilogram { get; } = new(88);

            public static UnitConverterUnits WeightLongTon { get; } = new(89);

            public static UnitConverterUnits WeightMilligram { get; } = new(90);

            public static UnitConverterUnits WeightOunce { get; } = new(91);

            public static UnitConverterUnits WeightPound { get; } = new(92);

            public static UnitConverterUnits WeightShortTon { get; } = new(93);

            public static UnitConverterUnits WeightStone { get; } = new(94);

            public static UnitConverterUnits WeightTonne { get; } = new(95);

            public static UnitConverterUnits AreaSoccerField { get; } = new(99);

            public static UnitConverterUnits DataFloppyDisk { get; } = new(100);

            public static UnitConverterUnits DataCD { get; } = new(101);

            public static UnitConverterUnits DataDVD { get; } = new(102);

            public static UnitConverterUnits EnergyBattery { get; } = new(103);

            public static UnitConverterUnits LengthPaperclip { get; } = new(105);

            public static UnitConverterUnits LengthJumboJet { get; } = new(107);

            public static UnitConverterUnits PowerLightBulb { get; } = new(108);

            public static UnitConverterUnits PowerHorse { get; } = new(109);

            public static UnitConverterUnits VolumeBathtub { get; } = new(111);

            public static UnitConverterUnits WeightSnowflake { get; } = new(113);

            public static UnitConverterUnits WeightElephant { get; } = new(114);

            public static UnitConverterUnits VolumeTeaspoonUK { get; } = new(115);

            public static UnitConverterUnits VolumeTablespoonUK { get; } = new(116);

            public static UnitConverterUnits AreaHand { get; } = new(118);

            public static UnitConverterUnits SpeedTurtle { get; } = new(121);

            public static UnitConverterUnits SpeedJet { get; } = new(122);

            public static UnitConverterUnits VolumeCoffeeCup { get; } = new(124);

            public static UnitConverterUnits WeightWhale { get; } = new(123);

            public static UnitConverterUnits VolumeSwimmingPool { get; } = new(125);

            public static UnitConverterUnits SpeedHorse { get; } = new(126);

            public static UnitConverterUnits AreaPaper { get; } = new(127);

            public static UnitConverterUnits AreaCastle { get; } = new(128);

            public static UnitConverterUnits EnergyBanana { get; } = new(129);

            public static UnitConverterUnits EnergySliceOfCake { get; } = new(130);

            public static UnitConverterUnits LengthHand { get; } = new(131);

            public static UnitConverterUnits PowerTrainEngine { get; } = new(132);

            public static UnitConverterUnits WeightSoccerBall { get; } = new(133);

            public static UnitConverterUnits AngleDegree { get; } = new(134);

            public static UnitConverterUnits AngleRadian { get; } = new(135);

            public static UnitConverterUnits AngleGradian { get; } = new(136);

            public static UnitConverterUnits PressureAtmosphere { get; } = new(137);

            public static UnitConverterUnits PressureBar { get; } = new(138);

            public static UnitConverterUnits PressureKiloPascal { get; } = new(139);

            public static UnitConverterUnits PressureMillimeterOfMercury { get; } = new(140);

            public static UnitConverterUnits PressurePascal { get; } = new(141);

            public static UnitConverterUnits PressurePSI { get; } = new(142);

            public static UnitConverterUnits DataExabits { get; } = new(143);

            public static UnitConverterUnits DataExabytes { get; } = new(144);

            public static UnitConverterUnits DataExbibits { get; } = new(145);

            public static UnitConverterUnits DataExbibytes { get; } = new(146);

            public static UnitConverterUnits DataGibibits { get; } = new(147);

            public static UnitConverterUnits DataGibibytes { get; } = new(148);

            public static UnitConverterUnits DataKibibits { get; } = new(149);

            public static UnitConverterUnits DataKibibytes { get; } = new(150);

            public static UnitConverterUnits DataMebibits { get; } = new(151);

            public static UnitConverterUnits DataMebibytes { get; } = new(152);

            public static UnitConverterUnits DataPebibits { get; } = new(153);

            public static UnitConverterUnits DataPebibytes { get; } = new(154);

            public static UnitConverterUnits DataTebibits { get; } = new(155);

            public static UnitConverterUnits DataTebibytes { get; } = new(156);

            public static UnitConverterUnits DataYobibits { get; } = new(157);

            public static UnitConverterUnits DataYobibytes { get; } = new(158);

            public static UnitConverterUnits DataYottabit { get; } = new(159);

            public static UnitConverterUnits DataYottabyte { get; } = new(160);

            public static UnitConverterUnits DataZebibits { get; } = new(161);

            public static UnitConverterUnits DataZebibytes { get; } = new(162);

            public static UnitConverterUnits DataZetabits { get; } = new(163);

            public static UnitConverterUnits DataZetabytes { get; } = new(164);

            public static UnitConverterUnits AreaPyeong { get; } = new(165);

            public static UnitConverterUnits EnergyKilowatthour { get; } = new(166);

            public static UnitConverterUnits DataNibble { get; } = new(167);

            public static UnitConverterUnits LengthAngstrom { get; } = new(168);

            public static UnitConverterUnits UnitEnd { get; } = LengthAngstrom;
        }
    }
}
