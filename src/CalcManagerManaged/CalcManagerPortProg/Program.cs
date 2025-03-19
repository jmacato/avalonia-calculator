// See https://aka.ms/new-console-template for more information

using CalcManagerPort;

Console.WriteLine("Hello, World!");
var precision = 64;
var k = new CalcManagerPort.RatPak();
var num1pi = k.StringToNumber("1584525424547797615479348427121", 10, precision);
var num2pi = k.StringToNumber("504370107543132052380609147136", 10, precision);
var num1 = k.StringToNumber("2", 10, precision);
var num2 = k.StringToNumber("3", 10, precision);
var num3 = k.StringToNumber("3", 10, precision);
var num4 = k.StringToNumber("7", 10, precision);
//2/3 × 3/7
void DisplayNum(RatPak.NUMBER dig)
{
    Console.WriteLine($"MANTISSA: {string.Join(',',dig.mant.Select(x=>x.ToString()))}");
    Console.WriteLine($"SIGN: {dig.sign}");
    Console.WriteLine($"cdigit: {dig.cdigit}");
    Console.WriteLine($"exp: {dig.exp}");
}

var rat1 = k.numtorat(num1, 10);
var rat2 = k.numtorat(num2, 10);
var rat3 = k.numtorat(num3, 10);
var rat4 = k.numtorat(num4, 10);

k.divrat(ref rat1, rat2, precision);
k.divrat(ref rat3, rat4, precision);
k.mulrat(ref rat1, rat3, precision);

Console.WriteLine(k.RatToString(ref rat1, RatPak.NumberFormat.Float, 10, precision));
Console.WriteLine(k.RatToString(ref k.pi, RatPak.NumberFormat.Float, 10, precision));
Console.WriteLine(k.RatToString(ref k.two_pi, RatPak.NumberFormat.Float, 10, precision));
Console.WriteLine(k.RatToString(ref k.pi_over_two, RatPak.NumberFormat.Float, 10, precision));
Console.WriteLine(k.RatToString(ref k.one_pt_five_pi, RatPak.NumberFormat.Float, 10, precision));
Console.WriteLine(k.RatToString(ref k.e_to_one_half, RatPak.NumberFormat.Float, 10, precision));
Console.WriteLine(k.RatToString(ref k.rad_to_deg, RatPak.NumberFormat.Float, 10, precision));
Console.WriteLine(k.RatToString(ref k.rad_to_grad, RatPak.NumberFormat.Float, 10, precision));
Console.WriteLine(k.RatToString(ref k.pt_eight_five, RatPak.NumberFormat.Float, 10, precision));
