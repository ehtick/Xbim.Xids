﻿using IdsLib.IfcSchema;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Xbim.Common.Metadata;
using Xbim.InformationSpecifications.Helpers;

namespace Xbim.InformationSpecifications.Generator.Measures
{
    internal class MeasureAutomation
    {
        static public IEnumerable<Measure> GetFromDocumentation()
        {
            var splitter = new string[] { "|" };
            FileInfo f = new("Files/Units.md");
            var allDocumentation = File.ReadAllLines(f.FullName);
            var isParsing = false;
            var tally = 0;
            foreach (var oneLine in allDocumentation)
            {
                if (isParsing)
                {
                    var parts = oneLine.Split(splitter, StringSplitOptions.TrimEntries);
                    if (parts.Length != 8) // set here the amount of fields expected 
                    {
                        // we are leaving the loop, check the expected tally
                        if (tally != 83) // need to review the info from the documentation
                            throw new Exception("Unexpected number of measures.");
                        yield break; // no more measures to find.
                    }
                    var retMeasurement = new Measure()
                    {
                        IfcMeasure = parts[1],
                        Description = parts[2],
                        Unit = parts[3],
                        UnitSymbol = parts[4],
                        DimensionalExponents = parts[5],
                        UnitEnum = parts[6],
                    };
                    tally++;
                    yield return retMeasurement;
                }
                else
                {
                    if (oneLine.Contains("-----"))
                    {
                        isParsing = true;
                        continue; // start parsing
                    }
                }
            }
        }

        /// <summary>
        /// Gets measures from the documentation and tries to fill in missing dimensional exponents
        /// </summary>
        public static string Execute_ImproveDocumentation()
        {
            MeasureCollection m = new(GetFromDocumentation());

            bool tryImprove = true;
            while (tryImprove)
            {
                tryImprove = false;
                foreach (var missingExp in m.MeasureList.Where(x => x.DimensionalExponents == ""))
                {
                    Debug.WriteLine($"Trying to resolve: {missingExp.IfcMeasure} - {missingExp.UnitSymbol}");
                    if (string.IsNullOrEmpty(missingExp.UnitSymbol))
                        continue;
                    var neededSymbols = UnitFactor.SymbolBreakDown(missingExp.UnitSymbol);
                    var allSym = true;
                    foreach (var sym in neededSymbols)
                    {
                        var foundSymbol = m.GetByUnit(sym.UnitSymbol);
                        if (foundSymbol != null)
                        {
                            if (foundSymbol.DimensionalExponents != "")
                            {
                                if (sym.TryGetDimensionalExponents(out var tde, out _, out _))
                                    Debug.WriteLine($"- Found '{foundSymbol.UnitSymbol}' - {foundSymbol.DimensionalExponents} - {tde.ToUnitSymbol()}");
                                else
                                    throw new Exception("This needs to work.");
                            }
                            else
                            {
                                Debug.WriteLine($" - Missing dimensional exponents on '{foundSymbol.UnitSymbol}'");
                                allSym = false;
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"- Missing '{sym.UnitSymbol}' - {missingExp.UnitSymbol}");
                            allSym = false;
                        }
                    }
                    if (allSym)
                    {
                        DimensionalExponents? d = null;
                        Debug.WriteLine($"Can do {missingExp.Description} - {missingExp.UnitSymbol}");
                        foreach (var sym in neededSymbols)
                        {
                            var found = m.GetByUnit(sym.UnitSymbol) ?? throw new Exception("unexpected unit");
                            DimensionalExponents? thisDE = null;
                            if (!string.IsNullOrEmpty(found.DimensionalExponents))
                                thisDE = DimensionalExponents.FromString(found.DimensionalExponents);
                            else
                                sym.TryGetDimensionalExponents(out thisDE, out _, out _);
                            if (thisDE is null)
                                throw new Exception();
                            
                            if (d == null)
                                d = thisDE;
                            else
                            {
                                d = d.Multiply(thisDE);
                            }
                        }
                        if (d != null)
                        {
                            Debug.WriteLine($"Computed: {d} - {d.ToUnitSymbol()}");
                            missingExp.DimensionalExponents = d.ToString();
                            tryImprove = true;
                        }
                        Debug.WriteLine("");
                    }
                    else
                    {
                        Debug.WriteLine($"- Cannot do {missingExp.IfcMeasure}, {missingExp.Description} - {missingExp.UnitSymbol}\r\n");
                    }
                }
            }
            var sb = new StringBuilder();
            sb.AppendLine("Entire list");
            foreach (var item in m.MeasureList)
            {
                sb.AppendLine($"| {item.IfcMeasure} | {item.Description} | {item.Unit} | {item.UnitSymbol} | {item.DimensionalExponents} | {item.UnitEnum} |");
            }
            Debug.WriteLine(sb.ToString());
            return sb.ToString();
        }

        private static string NewStringArray(string[] classes)
        {
            return @$"new[] {{ ""{string.Join("\", \"", classes)}"" }}";
        }

        public static string Execute_GenerateIfcMeasureDictionary()
        {
            var source = stub;
            var sb = new StringBuilder();

            var schemas = new[] { Xbim.Properties.Version.IFC2x3, Xbim.Properties.Version.IFC4, Properties.Version.IFC4x3 };
            var meta = new List<ExpressMetaData>();

            foreach (var schema in schemas)
            {
                System.Reflection.Module module = SchemaHelper.GetModule(schema);
                var metaD = ExpressMetaData.GetMetadata(module);
                meta.Add(metaD);
            }

            var allUnits = GetSchemaUnits();

            MeasureCollection mCollection = new(GetFromDocumentation().Concat(ExtraMeasures()));
            foreach (var measure in mCollection.MeasureList)
            {
                var concreteClasses = new List<string>();
                
                var expectedUnitsTypes = new[] {
                    measure.Description.ToUpperInvariant().Replace(" ", "") + "UNIT",
                    measure.IfcMeasure[3..^7].ToUpperInvariant() + "UNIT",
                    //measure.Key.ToUpperInvariant() + "VALUEUNIT",
                    //"THERMODYNAMIC" + measure.Key.ToUpperInvariant() + "UNIT",
                    };
                expectedUnitsTypes = expectedUnitsTypes.Distinct().ToArray();
                // special cases
                //
                //if (measure.Key == "Speed")
                //    expectedUnitsTypes = new[] { "LINEARVELOCITYUNIT" }; 
                if (measure.IfcMeasure == "IfcThermalConductivityMeasure")
                    expectedUnitsTypes = new[] { "THERMALCONDUCTANCEUNIT" };
                //else if (measure.Key == "Heating")
                //    expectedUnitsTypes = new[] { "HEATINGVALUEUNIT" };
                //else if (measure.Key == "Temperature")
                //    expectedUnitsTypes = new[] { "THERMODYNAMICTEMPERATUREUNIT"};
                //else if (measure.Key == "Angle")
                //    expectedUnitsTypes = new[] { "PLANEANGLEUNIT" };

                var expectedUnitType = "";
                foreach (var item in expectedUnitsTypes)
                {
                    var t = allUnits.FirstOrDefault(x => x.EndsWith($".{item}"));
                    if (t is not null)
                    {
                        expectedUnitType = t;
                        break;
                    }
                }
                
                var ifcType = measure.IfcMeasure;
                if (ifcType != null)
                {
                    for (int i = 0; i < schemas.Length; i++)
                    {
                        _ = schemas[i];
                        var metaD = meta[i];
                        var tp = metaD.ExpressType(ifcType.ToUpperInvariant());
                        if (tp != null)
                        {
                            var cClass = tp.Type.FullName!.Replace("Xbim.", "");
                            concreteClasses.Add(cClass);
                        }
                    }
                }
                sb.AppendLine($"\t\t\t{{ \"{measure.IfcMeasure}\", new IfcMeasureInfo(\"{measure.IfcMeasure}\", \"{measure.Description}\", \"{measure.Unit}\", \"{measure.UnitSymbol}\", \"{measure.DimensionalExponents}\", {NewStringArray(concreteClasses.ToArray())}, \"{expectedUnitType}\") }},");
            }

            source = source.Replace($"\t\t\t<PlaceHolder>\r\n", sb.ToString());
            source = source.Replace($"<PlaceHolderVersion>", VersionHelper.GetFileVersion(typeof(ExpressMetaData)));
            return source;
        }

        private static IList<string> GetSchemaUnits()
        {
            var all = Enum.GetValues<Ifc2x3.MeasureResource.IfcDerivedUnitEnum>().Select(x => $"IfcDerivedUnitEnum.{x}");
            all = all.Union(Enum.GetValues<Ifc4.Interfaces.IfcDerivedUnitEnum>().Select(x => $"IfcDerivedUnitEnum.{x}"));
            all = all.Union(Enum.GetValues<Ifc4x3.MeasureResource.IfcDerivedUnitEnum>().Select(x => $"IfcDerivedUnitEnum.{x}"));
            all = all.Union(Enum.GetValues<Ifc2x3.MeasureResource.IfcUnitEnum>().Select(x => $"IfcUnitEnum.{x}"));
            all = all.Union(Enum.GetValues<Ifc4.Interfaces.IfcUnitEnum>().Select(x => $"IfcUnitEnum.{x}"));
            all = all.Union(Enum.GetValues<Ifc4x3.MeasureResource.IfcUnitEnum>().Select(x => $"IfcUnitEnum.{x}"));

            return all.Distinct().OrderBy(x=>x).ToList();
        }


        public static (string, string)[] ExtraMeasureTypes = new[]
        {
            ("IfcText", "A string"),
            ("IfcIdentifier", "An identifier expressed as string"),
        };

        public static string Execute_GenerateIfcMeasureEnum()
        {
            var source = stubEnums;
            var sb = new StringBuilder();

            // an enum used to be defined in the schema, now it's a simple text
            foreach (var measure in IdsLib.IfcSchema.SchemaInfo.AllMeasureInformation)
            {
                if (measure.Exponents != null)
                {
                    sb.AppendLine($"\t\t/// {measure.Description}, expressed in {measure.GetUnit()}");
                }
                else
                {
                    sb.AppendLine($"\t\t/// {measure}, no unit conversion");
                }

                sb.AppendLine($"\t\t{measure.Id},");
            }
            foreach (var measure in ExtraMeasureTypes)
            {
                sb.AppendLine($"\t\t/// {measure.Item2},");
                sb.AppendLine($"\t\t{measure.Item1},");
            }

            source = Regex.Replace(source, $"[\t ]*<PlaceHolder>", sb.ToString());
            source = source.Replace($"<PlaceHolderVersion>", VersionHelper.GetFileVersion(typeof(ExpressMetaData)));
            return source;
        }

        private static IEnumerable<Measure> ExtraMeasures()
        {
            yield break;
            //yield return new Measure() { IfcMeasure = "String" };
            //yield return new Measure() { Key = "Number" };
        }


        /// <summary>
        /// ensures that the measure helpers are fully populated
        /// </summary>
        /// <returns>False if no warnings</returns>
        public static bool Execute_CheckMeasureMetadata()
        {
            foreach (var measVal in IdsLib.IfcSchema.SchemaInfo.AllMeasureInformation)
            {
                if (measVal.UnitTypeEnum == "")
                    Program.Message(ConsoleColor.DarkYellow, $"Warning: Measure '{measVal.Id}' lacks UnitType.");
                // Debug.WriteLine($"{measVal.UnitTypeEnum}");
            }
            return false;
        }

        private const string stub = @"// SchemaInfo.IfcMeasures.cs is automatically generated by xbim.xids.generator.Execute_GenerateIfcMeasureDictionary() using Xbim.Essentials <PlaceHolderVersion>
using System.Collections.Generic;
using System;

namespace Xbim.InformationSpecifications.Helpers
{
	public partial class SchemaInfo
	{
		/// <summary>
		/// Repository of valid <see cref=""IfcMeasureInfo""/> metadata given the persistence string defined in bS IDS
		/// </summary>
        [Obsolete(""Use idslib information instead"")]
		public static Dictionary<string, IfcMeasureInfo> IfcMeasures { get; } = new()
		{
			<PlaceHolder>
		};
	}
}
";

        private const string stubEnums = @"// Enums.generated.cs is automatically generated by xbim.xids.generator.Execute_GenerateIfcMeasureDictionary() using Xbim.Essentials <PlaceHolderVersion>
using System;
using System.Collections.Generic;
using System.Text;

namespace Xbim.InformationSpecifications.Helpers
{
    /// <summary>
    /// Determines data type constraints and conversion for measures.
    /// </summary>
    [Obsolete(""Should use the IdsLib.IfcSchema.SchemaInfo.IfcMeasureInformation instead."")]
    public enum IfcValue
    {
        <PlaceHolder>
    }
}
";
    }
}