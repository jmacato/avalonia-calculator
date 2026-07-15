// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/****************************Module*Header***********************************
* Module Name: EngineStrings.h
*
* Module Description:
*       Resource String ID's for the private strings used by Engine. Internal to Engine related code
*         not required by the clients
*
* Warnings:
*
* Created: 13-Feb-2008
*
\****************************************************************************/

namespace CalcEngine;

public static class EngineStrings
{
    public const int IdsErrorsFirst = 99;

    // This is the list of error strings corresponding to SCERR_DIVIDEZERO..

    public const int IdsDivbyzero = IdsErrorsFirst;
    public const int IdsDomain = IdsErrorsFirst + 1;
    public const int IdsUndefined = IdsErrorsFirst + 2;
    public const int IdsPosInfinity = IdsErrorsFirst + 3;
    public const int IdsNegInfinity = IdsErrorsFirst + 4;
    public const int IdsNomem = IdsErrorsFirst + 6;
    public const int IdsToomany = IdsErrorsFirst + 7;
    public const int IdsOverflow = IdsErrorsFirst + 8;
    public const int IdsNoresult = IdsErrorsFirst + 9;
    public const int IdsInsufficientData = IdsErrorsFirst + 10;

    public const int CSTRINGSENGMAX = IdsInsufficientData + 1;

    // Arithmetic expression evaluator error strings
    public const int IdsErrUnkCh = CSTRINGSENGMAX + 1;
    public const int IdsErrUnkFn = CSTRINGSENGMAX + 2;
    public const int IdsErrUnexNum = CSTRINGSENGMAX + 3;
    public const int IdsErrUnexCh = CSTRINGSENGMAX + 4;
    public const int IdsErrUnexSz = CSTRINGSENGMAX + 5;
    public const int IdsErrMismatchClose = CSTRINGSENGMAX + 6;
    public const int IdsErrUnexEnd = CSTRINGSENGMAX + 7;
    public const int IdsErrSgInvError = CSTRINGSENGMAX + 8;
    public const int IdsErrInputOverflow = CSTRINGSENGMAX + 9;
    public const int IdsErrOutputOverflow = CSTRINGSENGMAX + 10;

    // Resource keys for CEngineStrings.resw
    public const string SidsPlusMinus = "0";
    public const string SidsClear = "1";
    public const string SidsCe = "2";
    public const string SidsBackspace = "3";
    public const string SidsDecimalSeparator = "4";
    public const string SidsEmptyString = "5";
    public const string SidsAnd = "6";
    public const string SidsOr = "7";
    public const string SidsXor = "8";
    public const string SidsLsh = "9";
    public const string SidsRsh = "10";
    public const string SidsDivide = "11";
    public const string SidsMultiply = "12";
    public const string SidsPlus = "13";
    public const string SidsMinus = "14";
    public const string SidsMod = "15";
    public const string SidsYroot = "16";
    public const string SidsPowHat = "17";
    public const string SidsInt = "18";
    public const string SidsRol = "19";
    public const string SidsRor = "20";
    public const string SidsNot = "21";
    public const string SidsSin = "22";
    public const string SidsCos = "23";
    public const string SidsTan = "24";
    public const string SidsSinh = "25";
    public const string SidsCosh = "26";
    public const string SidsTanh = "27";
    public const string SidsLn = "28";
    public const string SidsLog = "29";
    public const string SidsSqrt = "30";
    public const string SidsXpow2 = "31";
    public const string SidsXpow3 = "32";
    public const string SidsNfactorial = "33";
    public const string SidsReciprocal = "34";
    public const string SidsDms = "35";
    public const string SidsPowten = "37";
    public const string SidsPercent = "38";
    public const string SidsScientificNotation = "39";
    public const string SidsPi = "40";
    public const string SidsEqual = "41";
    public const string SidsMc = "42";
    public const string SidsMr = "43";
    public const string SidsMs = "44";
    public const string SidsMplus = "45";
    public const string SidsMminus = "46";
    public const string SidsExp = "47";
    public const string SidsOpenParen = "48";
    public const string SidsCloseParen = "49";
    public const string Sids0 = "50";
    public const string Sids1 = "51";
    public const string Sids2 = "52";
    public const string Sids3 = "53";
    public const string Sids4 = "54";
    public const string Sids5 = "55";
    public const string Sids6 = "56";
    public const string Sids7 = "57";
    public const string Sids8 = "58";
    public const string Sids9 = "59";
    public const string SidsA = "60";
    public const string SidsB = "61";
    public const string SidsC = "62";
    public const string SidsD = "63";
    public const string SidsE = "64";
    public const string SidsF = "65";
    public const string SidsFrac = "66";
    public const string SidsSind = "67";
    public const string SidsCosd = "68";
    public const string SidsTand = "69";
    public const string SidsAsind = "70";
    public const string SidsAcosd = "71";
    public const string SidsAtand = "72";
    public const string SidsSinr = "73";
    public const string SidsCosr = "74";
    public const string SidsTanr = "75";
    public const string SidsAsinr = "76";
    public const string SidsAcosr = "77";
    public const string SidsAtanr = "78";
    public const string SidsSing = "79";
    public const string SidsCosg = "80";
    public const string SidsTang = "81";
    public const string SidsAsing = "82";
    public const string SidsAcosg = "83";
    public const string SidsAtang = "84";
    public const string SidsAsinh = "85";
    public const string SidsAcosh = "86";
    public const string SidsAtanh = "87";
    public const string SidsPowe = "88";
    public const string SidsPowten2 = "89";
    public const string SidsSqrt2 = "90";
    public const string SidsSqr = "91";
    public const string SidsCube = "92";
    public const string SidsCubert = "93";
    public const string SidsFact = "94";
    public const string SidsReciproc = "95";
    public const string SidsDegrees = "96";
    public const string SidsNegate = "97";
    public const string SidsRsh2 = "98";
    public const string SidsDividebyzero = "99";
    public const string SidsDomain = "100";
    public const string SidsUndefined = "101";
    public const string SidsPosInfinity = "102";
    public const string SidsNegInfinity = "103";
    public const string SidsAborted = "104";
    public const string SidsNomem = "105";
    public const string SidsToomany = "106";
    public const string SidsOverflow = "107";
    public const string SidsNoresult = "108";

    public const string SidsInsufficientData = "109";

    // 110 is skipped by CSTRINGSENGMAX
    public const string SidsErrUnkCh = "111";
    public const string SidsErrUnkFn = "112";
    public const string SidsErrUnexNum = "113";
    public const string SidsErrUnexCh = "114";
    public const string SidsErrUnexSz = "115";
    public const string SidsErrMismatchClose = "116";
    public const string SidsErrUnexEnd = "117";
    public const string SidsErrSgInvError = "118";
    public const string SidsErrInputOverflow = "119";
    public const string SidsErrOutputOverflow = "120";
    public const string SidsSecd = "SecDeg";
    public const string SidsSecr = "SecRad";
    public const string SidsSecg = "SecGrad";
    public const string SidsAsecd = "InverseSecDeg";
    public const string SidsAsecr = "InverseSecRad";
    public const string SidsAsecg = "InverseSecGrad";
    public const string SidsCscd = "CscDeg";
    public const string SidsCscr = "CscRad";
    public const string SidsCscg = "CscGrad";
    public const string SidsAcscd = "InverseCscDeg";
    public const string SidsAcscr = "InverseCscRad";
    public const string SidsAcscg = "InverseCscGrad";
    public const string SidsCotd = "CotDeg";
    public const string SidsCotr = "CotRad";
    public const string SidsCotg = "CotGrad";
    public const string SidsAcotd = "InverseCotDeg";
    public const string SidsAcotr = "InverseCotRad";
    public const string SidsAcotg = "InverseCotGrad";
    public const string SidsSech = "Sech";
    public const string SidsAsech = "InverseSech";
    public const string SidsCsch = "Csch";
    public const string SidsAcsch = "InverseCsch";
    public const string SidsCoth = "Coth";
    public const string SidsAcoth = "InverseCoth";
    public const string SidsTwopowx = "TwoPowX";
    public const string SidsLogbasey = "LogBaseY";
    public const string SidsAbs = "Abs";
    public const string SidsFloor = "Floor";
    public const string SidsCeil = "Ceil";
    public const string SidsNand = "Nand";
    public const string SidsNor = "Nor";
    public const string SidsCuberoot = "CubeRoot";
    public const string SidsProgrammerMod = "ProgrammerMod";

    // Include the resource key ID from above into this vector to load it into memory for the engine to use
    internal static string[] CreateResourceIds() =>
    [
        SidsPlusMinus,
        SidsC,
        SidsCe,
        SidsBackspace,
        SidsDecimalSeparator,
        SidsEmptyString,
        SidsAnd,
        SidsOr,
        SidsXor,
        SidsLsh,
        SidsRsh,
        SidsDivide,
        SidsMultiply,
        SidsPlus,
        SidsMinus,
        SidsMod,
        SidsYroot,
        SidsPowHat,
        SidsInt,
        SidsRol,
        SidsRor,
        SidsNot,
        SidsSin,
        SidsCos,
        SidsTan,
        SidsSinh,
        SidsCosh,
        SidsTanh,
        SidsLn,
        SidsLog,
        SidsSqrt,
        SidsXpow2,
        SidsXpow3,
        SidsNfactorial,
        SidsReciprocal,
        SidsDms,
        SidsPowten,
        SidsPercent,
        SidsScientificNotation,
        SidsPi,
        SidsEqual,
        SidsMc,
        SidsMr,
        SidsMs,
        SidsMplus,
        SidsMminus,
        SidsExp,
        SidsOpenParen,
        SidsCloseParen,
        Sids0,
        Sids1,
        Sids2,
        Sids3,
        Sids4,
        Sids5,
        Sids6,
        Sids7,
        Sids8,
        Sids9,
        SidsA,
        SidsB,
        SidsC,
        SidsD,
        SidsE,
        SidsF,
        SidsFrac,
        SidsSind,
        SidsCosd,
        SidsTand,
        SidsAsind,
        SidsAcosd,
        SidsAtand,
        SidsSinr,
        SidsCosr,
        SidsTanr,
        SidsAsinr,
        SidsAcosr,
        SidsAtanr,
        SidsSing,
        SidsCosg,
        SidsTang,
        SidsAsing,
        SidsAcosg,
        SidsAtang,
        SidsAsinh,
        SidsAcosh,
        SidsAtanh,
        SidsPowe,
        SidsPowten2,
        SidsSqrt2,
        SidsSqr,
        SidsCube,
        SidsCubert,
        SidsFact,
        SidsReciproc,
        SidsDegrees,
        SidsNegate,
        SidsRsh,
        SidsDividebyzero,
        SidsDomain,
        SidsUndefined,
        SidsPosInfinity,
        SidsNegInfinity,
        SidsAborted,
        SidsNomem,
        SidsToomany,
        SidsOverflow,
        SidsNoresult,
        SidsInsufficientData,
        SidsErrUnkCh,
        SidsErrUnkFn,
        SidsErrUnexNum,
        SidsErrUnexCh,
        SidsErrUnexSz,
        SidsErrMismatchClose,
        SidsErrUnexEnd,
        SidsErrSgInvError,
        SidsErrInputOverflow,
        SidsErrOutputOverflow,
        SidsSecd,
        SidsSecg,
        SidsSecr,
        SidsAsecd,
        SidsAsecr,
        SidsAsecg,
        SidsCscd,
        SidsCscr,
        SidsCscg,
        SidsAcscd,
        SidsAcscr,
        SidsAcscg,
        SidsCotd,
        SidsCotr,
        SidsCotg,
        SidsAcotd,
        SidsAcotr,
        SidsAcotg,
        SidsSech,
        SidsAsech,
        SidsCsch,
        SidsAcsch,
        SidsCoth,
        SidsAcoth,
        SidsTwopowx,
        SidsLogbasey,
        SidsAbs,
        SidsFloor,
        SidsCeil,
        SidsNand,
        SidsNor,
        SidsCuberoot,
        SidsProgrammerMod,
    ];
}
