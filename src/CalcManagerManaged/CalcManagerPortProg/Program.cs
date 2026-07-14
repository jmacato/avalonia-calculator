// See https://aka.ms/new-console-template for more information

using CalcEngine;

var precision = 64;
var k = new RatPak();
var num1pi = k.StringToNumber("1584525424547797615479348427121", 10, precision);
var num2pi = k.StringToNumber("504370107543132052380609147136", 10, precision);
var num1 = k.StringToNumber("2", 10, precision);
var num2 = k.StringToNumber("3", 10, precision);
var num3 = k.StringToNumber("3", 10, precision);
var num4 = k.StringToNumber("7", 10, precision);
//2/3 × 3/7
ArgumentNullException.ThrowIfNull(num1);
ArgumentNullException.ThrowIfNull(num2);
ArgumentNullException.ThrowIfNull(num3);
ArgumentNullException.ThrowIfNull(num4);

var rat1 = RatPak.numtorat(num1, 10);
var rat2 = RatPak.numtorat(num2, 10);
var rat3 = RatPak.numtorat(num3, 10);
var rat4 = RatPak.numtorat(num4, 10);

k.divrat(ref rat1, rat2, precision);
k.divrat(ref rat3, rat4, precision);
k.mulrat(ref rat1, rat3, precision);

Console.WriteLine(k.RatToString(ref rat1, NumberFormat.FloatingPoint, 10, precision));
var piRat = k.Pi;
Console.WriteLine(k.RatToString(ref piRat, NumberFormat.FloatingPoint, 10, precision));
var twoPiRat = k.TwoPi;
Console.WriteLine(k.RatToString(ref twoPiRat, NumberFormat.FloatingPoint, 10, precision));
var piOverTwoRat = k.PiOverTwo;
Console.WriteLine(k.RatToString(ref piOverTwoRat, NumberFormat.FloatingPoint, 10, precision));
var onePtFivePiRat = k.OnePtFivePi;
Console.WriteLine(k.RatToString(ref onePtFivePiRat, NumberFormat.FloatingPoint, 10, precision));
var eToOneHalfRat = k.EToOneHalf;
Console.WriteLine(k.RatToString(ref eToOneHalfRat, NumberFormat.FloatingPoint, 10, precision));
var radToDegRat = k.RadToDeg;
Console.WriteLine(k.RatToString(ref radToDegRat, NumberFormat.FloatingPoint, 10, precision));
var radToGradRat = k.RadToGrad;
Console.WriteLine(k.RatToString(ref radToGradRat, NumberFormat.FloatingPoint, 10, precision));
var ptEightFiveRat = k.PtEightFive;
Console.WriteLine(k.RatToString(ref ptEightFiveRat, NumberFormat.FloatingPoint, 10, precision));
