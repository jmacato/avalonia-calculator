# Application Architecture

Calculator is a C# application built with [Avalonia][Avalonia]. It follows the
Model-View-ViewModel ([MVVM][MVVM]) design pattern and ships through desktop and browser front ends.
This document describes the layers shared by those front ends.

--------------------
## Table of Contents

* [View](#view)
    * [VisualStates](#visualstates)
    * [Data-Binding](#data-binding)
* [ViewModel](#viewmodel)
    * [PropertyChanged Events](#propertychanged-events)
* [Model](#model)

--------------------

## View

The View layer is contained in the [Calculator project][Calculator folder]. This project contains XAML files
and custom controls that support the UI. [App.xaml][App.xaml] contains shared resources referenced by the views,
while [App.xaml.cs][App.xaml.cs] initializes application services and the active Avalonia lifetime.

[MainPage.xaml][MainPage.xaml] is the root container for the calculator UI. `MainPage` uses a
`NavigationView` control to display the toggleable navigation menu and hosts the active mode.
Of the many modes that Calculator shows in its menu, there are actually only three XAML files that `MainPage` needs to
manage in order to support all modes. They are:

* [Calculator.xaml][Calculator.xaml]: This [UserControl] is itself a container for the [Standard][CalculatorStandardOperators.xaml],
  [Scientific][CalculatorScientificOperators.xaml], and [Programmer][CalculatorProgrammerOperators.xaml] modes.
* [DateCalculator.xaml][DateCalculator.xaml]: Everything needed for the DateCalculator mode.
* [UnitConverter.xaml][UnitConverter.xaml]: One `UserControl` to support every Converter mode.

### VisualStates

[VisualStates][VisualState] are used to change the size, position, and appearance ([Style][Style]) of UI elements
in order to create an adaptive, responsive UI. A transition to a new `VisualState` is often triggered by specific
window sizes. Here are a few important examples of `VisualStates` in Calculator. Note that it is not a
complete list. When making UI changes, make sure you are considering the various `VisualStates` and layouts that
Calculator defines.

#### History/Memory Dock Panel expansion

In the Standard, Scientific, and Programmer modes, the History/Memory panel is exposed as a flyout in small window sizes.
Once the window is resized to have enough space, the panel becomes docked along the edge of the window.

<img src="Images\VisualStates\Standard1.gif" height="400" />

#### Scientific mode, inverse function button presence

In the Scientific mode, for small window sizes there is not enough room to show all the function buttons. The mode
hides some of the buttons and provides a Shift (↑) button to toggle the visibility of the collapsed rows. When the
window size is large enough, the buttons are re-arranged to display all function buttons at the same time.

<img src="Images\VisualStates\Scientific1.gif" height="400" />

#### Unit Converter aspect ratio adjustment

In the Unit Converter mode, the converter inputs and the numberpad will re-arrange depending on if the window is in
a Portrait or Landscape aspect ratio.

<img src="Images\VisualStates\Converter1.gif" height="400" />

### Data-Binding

Calculator uses [data binding][Data Binding] to update UI properties from its ViewModels.
Compiled bindings are enabled project-wide. Views should declare an `x:DataType` and use `{Binding ...}` so
binding paths are checked at build time and remain compatible with trimming and managed AOT.

------------
## ViewModel

The ViewModel layer is contained in the [ViewModels folder][ViewModels folder]. ViewModels provide a source of
data for the UI to bind against and act as the intermediary separating pure business logic from UI components that
should not care about the model's implementation. Just as the View layer consists of a hierarchy of XAML files, the
ViewModel consists of a hierarchy of ViewModel files. The relationship between XAML and ViewModel files is often 1:1.
Here are the notable ViewModel files to start exploring with:

* [ApplicationViewModel.cs][ApplicationViewModel.cs]: The ViewModel for [MainPage.xaml][MainPage.xaml]. This ViewModel
  is the root of the other mode-specific ViewModels. The application changes between modes by updating the `Mode` property
  of the `ApplicationViewModel`. The ViewModel will make sure the appropriate ViewModel for the new mode is initialized.
* [StandardCalculatorViewModel.cs][StandardCalculatorViewModel.cs]: The ViewModel for [Calculator.xaml][Calculator.xaml].
  This ViewModel exposes functionality for the main three Calculator modes: Standard, Scientific, and Programmer.
* [DateCalculatorViewModel.cs][DateCalculatorViewModel.cs]: The ViewModel for [DateCalculator.xaml][DateCalculator.xaml].
* [UnitConverterViewModel.cs][UnitConverterViewModel.cs]: The ViewModel for [UnitConverter.xaml][UnitConverter.xaml].
  This ViewModel implements the logic to support every converter mode, including Currency Converter.

### PropertyChanged Events

For [data binding](#data-binding) to work, ViewModels notify the XAML framework when a property changes.
Calculator ViewModels derive from [ViewModelBase.cs][ViewModelBase.cs], which uses CommunityToolkit.Mvvm's
`ObservableObject` implementation of [INotifyPropertyChanged][INotifyPropertyChanged].

Bindable properties generally use the toolkit's source generator. For example,
[ApplicationViewModel.cs][ApplicationViewModel.cs] declares:

```C#
[ObservableProperty]
private string _categoryName = string.Empty;
```

The generator creates the public `CategoryName` property and raises `PropertyChanged` only when its value changes.

--------
## Model

The Model for the Calculator modes is contained in the managed [CalcManagerPort project][CalcManager folder].
It consists of three layers: a `CalculatorManager`, which relies on a `CalcEngine`, which relies on the `Ratpack`.

### CalculatorManager

The CalculatorManager contains the logic for managing the overall Calculator app's data such as the History and Memory lists, as well as maintaining the instances of calculator engines used for the various modes. The implementation is defined in [CalculatorManager.cs][CalculatorManager.cs].

### CalcEngine

The CalcEngine contains the logic for interpreting and performing operations according to the commands passed to it. It maintains the current state of calculations and relies on the RatPack for performing mathematical operations. The managed engine is defined in [CCalcEngine.cs][CCalcEngine.cs].

### RatPack

The RatPack (short for Rational Pack) is the core of the Calculator model and contains the logic for
performing its mathematical operations (using [infinite precision][Infinite Precision] arithmetic
instead of regular floating point arithmetic). Its managed implementation is defined in [RatPak.cs][RatPak.cs].

[References]:####################################################################################################

[Avalonia]:                           https://docs.avaloniaui.net/
[MVVM]:                               https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/

[Calculator folder]:                  ../src/Calculator
[App.xaml]:                           ../src/Calculator/App.xaml
[App.xaml.cs]:                       ../src/Calculator/App.xaml.cs
[UserControl]:                        https://docs.avaloniaui.net/docs/reference/controls/usercontrol
[MainPage.xaml]:                      ../src/Calculator/Views/MainPage.xaml
[Calculator.xaml]:                    ../src/Calculator/Views/Calculator.xaml
[CalculatorStandardOperators.xaml]:   ../src/Calculator/Views/CalculatorStandardOperators.xaml
[CalculatorScientificOperators.xaml]: ../src/Calculator/Views/CalculatorScientificOperators.xaml
[CalculatorProgrammerOperators.xaml]: ../src/Calculator/Views/CalculatorProgrammerOperators.xaml
[DateCalculator.xaml]:                ../src/Calculator/Views/DateCalculator.xaml
[UnitConverter.xaml]:                 ../src/Calculator/Views/UnitConverter.xaml

[VisualState]:                        https://docs.avaloniaui.net/docs/basics/user-interface/styling/styles
[Style]:                              https://docs.avaloniaui.net/docs/basics/user-interface/styling/styles

[Data Binding]:                       https://docs.avaloniaui.net/docs/basics/data/data-binding/

[ViewModels folder]:                  ../src/Calculator/ViewModels
[ApplicationViewModel.cs]:            ../src/Calculator/ViewModels/ApplicationViewModel.cs
[StandardCalculatorViewModel.cs]:   ../src/Calculator/ViewModels/StandardCalculatorViewModel.cs
[DateCalculatorViewModel.cs]:       ../src/Calculator/ViewModels/DateCalculatorViewModel.cs
[UnitConverterViewModel.cs]:        ../src/Calculator/ViewModels/UnitConverterViewModel.cs
[ViewModelBase.cs]:                   ../src/Calculator/ViewModels/Common/ViewModelBase.cs

[INotifyPropertyChanged]:             https://learn.microsoft.com/dotnet/api/system.componentmodel.inotifypropertychanged

[CalcManager folder]:                 ../src/CalcManagerManaged/CalcManagerPort
[CalculatorManager.cs]:               ../src/CalcManagerManaged/CalcManagerPort/CalculatorManager.cs
[CCalcEngine.cs]:                     ../src/CalcManagerManaged/CalcManagerPort/CEngine/CCalcEngine.cs
[Infinite Precision]:                 https://en.wikipedia.org/wiki/Arbitrary-precision_arithmetic
[RatPak.cs]:                          ../src/CalcManagerManaged/CalcManagerPort/Ratpack/RatPak.cs
