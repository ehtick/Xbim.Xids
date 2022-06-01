﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
			FileInfo f = new("Files/Physical_Quantities_and_Units.md");
			var allDocumentation = File.ReadAllLines(f.FullName);
			var isParsing = false;
			var tally = 0;
			foreach (var oneLine in allDocumentation)
			{
				if (isParsing)
				{
					var parts = oneLine.Split(splitter, StringSplitOptions.TrimEntries);
					if (parts.Length != 9)
					{
						Debug.Assert(tally == 51); // need to review the info from the documentation
						yield break; // no more measures to find.
					}
					var retMeasurement = new Measure()
					{
						PhysicalQuantity = parts[1],
						Key = parts[2],
						Unit = parts[3],
						UnitSymbol = parts[4],
						IfcMeasure = parts[5],
						DimensionalExponents = parts[6],
						QUDT = parts[7],
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
					var neededSymbols = SymbolBreakDown(missingExp.UnitSymbol);
					var allSym = true;
					foreach (var sym in neededSymbols)
					{
						var found = m.GetByUnit(sym.UnitSymbol);
						if (found != null)
						{
							if (found.DimensionalExponents != "")
							{
								var tde = sym.GetDimensionalExponents(m);
								Debug.WriteLine($"Found '{found.UnitSymbol}' - {found.DimensionalExponents} - {tde.ToUnitSymbol()}");
							}
							else
							{
								Debug.WriteLine($"Missing dimensional exponents on '{found.UnitSymbol}'");
								allSym = false;
							}
						}
						else
						{
							Debug.WriteLine($"Missing '{sym.UnitSymbol}' - {missingExp.UnitSymbol}");
							allSym = false;
						}
					}
					if (allSym)
					{
						DimensionalExponents d = null;
						Debug.WriteLine($"Can do {missingExp.PhysicalQuantity} - {missingExp.UnitSymbol}");
						foreach (var sym in neededSymbols)
						{
							var found = m.GetByUnit(sym.UnitSymbol);
							if (d == null)
								d = sym.GetDimensionalExponents(m);
							else
								d = d.Multiply(sym.GetDimensionalExponents(m));
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
						Debug.WriteLine($"Cannot do {missingExp.PhysicalQuantity} - {missingExp.UnitSymbol}\r\n");
					}
				}
			}
			var sb = new StringBuilder();
			foreach (var item in m.MeasureList)
			{
				sb.AppendLine($"{item.PhysicalQuantity}\t{item.DimensionalExponents}");
			}
			Debug.WriteLine(sb.ToString());
			return sb.ToString();
		}

		private static string newStringArray(string[] classes)
		{
			return @$"new[] {{ ""{string.Join("\", \"", classes)}"" }}";
		}

		public static string Execute_GenerateIfcMeasureDictionary()
		{
			var source = stub;
			var sb = new StringBuilder();

			var schemas = new[] { Xbim.Properties.Version.IFC2x3, Xbim.Properties.Version.IFC4 };
			var meta = new List<ExpressMetaData>();

			foreach (var schema in schemas)
			{
				System.Reflection.Module module = null;
				if (schema == Properties.Version.IFC2x3)
					module = (typeof(Ifc2x3.Kernel.IfcProduct)).Module;
				else if (schema == Properties.Version.IFC4)
					module = (typeof(Ifc4.Kernel.IfcProduct)).Module;
				var metaD = ExpressMetaData.GetMetadata(module);
				meta.Add(metaD);
			}

			MeasureCollection mCollection = new(GetFromDocumentation().Concat(ExtraMeaures()));
			foreach (var measure in mCollection.MeasureList)
			{
				var concreteClasses = new List<string>();	
				var ifcType = measure.IfcMeasure;
				if (ifcType != null)
				{
					for (int i = 0; i < schemas.Length; i++)
					{
						Properties.Version schema = schemas[i];
						var metaD = meta[i];
						var tp = metaD.ExpressType(ifcType.ToUpperInvariant());
						if (tp != null)
						{
							var cClass = tp.Type.FullName.Replace("Xbim.", "");
							concreteClasses.Add(cClass);
						}
					}
				}
				sb.AppendLine($"\t\t\t{{ \"{measure.Key}\", new IfcMeasureInfo(\"{measure.Key}\", \"{measure.IfcMeasure}\", \"{measure.PhysicalQuantity}\", \"{measure.Unit}\", \"{measure.UnitSymbol}\", \"{measure.DimensionalExponents}\", {newStringArray(concreteClasses.ToArray())}) }},");
			}

			source = source.Replace($"\t\t\t<PlaceHolder>\r\n", sb.ToString());
			return source;
		}


		public static string Execute_GenerateIfcMeasureEnum()
		{
			var source = stubEnums;
			var sb = new StringBuilder();

			var doc = Program.GetBuildingSmartSchemaXML();
			var measureEnum = GetMeasureRestrictions(doc).ToList();
			foreach (var measure in measureEnum)
			{
				sb.AppendLine($"\t\t{measure},");
			}

			source = source.Replace($"\t\t<PlaceHolder>\r\n", sb.ToString());
			return source;
		}

		private static IEnumerable<Measure> ExtraMeaures()
		{
			yield return new Measure() { Key = "String" };
			yield return new Measure() { Key = "Number" };
		}

		/// <summary>
		/// ensures that the schema and the helpers are compatible.
		/// </summary>
		/// <returns>False if not compatible</returns>
		public static bool Execute_CheckMeasureEnumeration()
		{
			var doc = Program.GetBuildingSmartSchemaXML();
			var measureEnum = GetMeasureRestrictions(doc).ToList();
			var errors = false;
			foreach (var item in measureEnum)
			{
				if (!SchemaInfo.IfcMeasures.TryGetValue(item, out _))
				{
					Program.Message(ConsoleColor.Red, $"Value not found in helpers for measure '{item}'");
					errors = true;
				}
			}
			return errors;
		}

		private static IEnumerable<string> GetMeasureRestrictions(XmlDocument doc)
		{
			XmlNode root = doc.DocumentElement;
			var prop = root.ChildNodes.Cast<XmlNode>().FirstOrDefault(n => n.Attributes[0].Value == "propertyType");
			var measure = prop.ChildNodes.Cast<XmlNode>().FirstOrDefault(n => n.Attributes.Count > 0 && n.Attributes[0].Value == "measure");
			var tp = measure.ChildNodes.Cast<XmlNode>().FirstOrDefault(n => n.Name == "xs:simpleType");
			var rest = tp.ChildNodes.Cast<XmlNode>().FirstOrDefault(n => n.Name == "xs:restriction");
			foreach (var item in rest.ChildNodes.Cast<XmlNode>())
			{
				yield return item.Attributes[0].Value;
			}
		}


		private static IEnumerable<UnitFactor> SymbolBreakDown(string unitSymbol)
		{
			var fraction = unitSymbol.Split('/');
			var num = fraction[0];
			var den = fraction.Length == 2 ? fraction[1] : "";

			var numUnits = num.Split(' ');
			var denUnits = den.Split(' ');

			foreach (var item in numUnits.Where(x => x != ""))
				yield return  new UnitFactor(item);
			foreach (var item in denUnits.Where(x=>x!=""))
				yield return new UnitFactor(item).Invert();
		}
 
		private const string stub = @"// generated running xbim.xids.generator
using System.Collections.Generic;

namespace Xbim.InformationSpecifications.Helpers
{
	public partial class SchemaInfo
	{
		public static Dictionary<string, IfcMeasureInfo> IfcMeasures = new()
		{
			<PlaceHolder>
		};
	}
}
";

		private const string stubEnums = @"// generated running xbim.xids.generator
using System;
using System.Collections.Generic;
using System.Text;

namespace Xbim.InformationSpecifications.Helpers
{
    public enum IfcMeasures
    {
		<PlaceHolder>
    }
}
";
	}
}