﻿#pragma warning disable IDE0028
using IdsLib.IfcSchema;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Xbim.InformationSpecifications.Cardinality;
using Xbim.InformationSpecifications.Facets.buildingSMART;
using Xbim.InformationSpecifications.Helpers;

namespace Xbim.InformationSpecifications
{
	public partial class Xids
	{
		private static XmlWriterSettings WriteSettings
		{
			get
			{
				var settings = new XmlWriterSettings
				{
					Async = false
				};
				if (PrettyOutput)
				{
					settings.Indent = true;
					settings.IndentChars = "\t";
				}

				return settings;
			}
		}

		/// <summary>
		/// Determines if the XML output should be indented for readability
		/// </summary>
		public static bool PrettyOutput { get; set; } = true;

		/// <summary>
		/// When exporting to bS IDS, export files can be one of the formats in this enum.
		/// </summary>
		public enum ExportedFormat
		{
			/// <summary>
			/// A single IDS in XML format
			/// </summary>
			XML,
			/// <summary>
			/// multiple IDS files in a compressed ZIP format
			/// </summary>
			ZIP
		}

		/// <summary>
		/// Exports the entire XIDS to a buildingSmart file, depending on the number of groups exports an XML or a ZIP file.
		/// </summary>
		/// <param name="destinationFileName">the path of a writeable location on disk</param>
		/// <param name="logger">the logging context</param>
		/// <returns>An enum determining if XML or ZIP files were written</returns>
		public ExportedFormat ExportBuildingSmartIDS(string destinationFileName, ILogger? logger = null)
		{
			if (File.Exists(destinationFileName))
			{
				var f = new FileInfo(destinationFileName);
				logger?.LogInformation("File is being overwritten: {file}", f.FullName);
				File.Delete(destinationFileName);
			}
			using FileStream fs = File.Create(destinationFileName);
			return ExportBuildingSmartIDS(fs, logger);
		}

		/// <summary>
		/// Exports the entire XIDS to a buildingSmart file, depending on the number of groups exports an XML or a ZIP file.
		/// </summary>
		/// <param name="destinationStream">a writeable stream</param>
		/// <param name="logger">the logging context</param>
		/// <returns>An enum determining if XML or ZIP files were written</returns>
		public ExportedFormat ExportBuildingSmartIDS(Stream destinationStream, ILogger? logger = null)
		{
			if (SpecificationsGroups.Count == 1)
			{

				using XmlWriter writer = XmlWriter.Create(destinationStream, WriteSettings);
				ExportBuildingSmartIDS(SpecificationsGroups.First(), writer, logger);
				writer.Close();
				return ExportedFormat.XML;
			}

			using var zipArchive = new ZipArchive(destinationStream, ZipArchiveMode.Create, true);
			int i = 0;
			foreach (var specGroup in SpecificationsGroups)
			{

				var name = (string.IsNullOrEmpty(specGroup.Name))
					? $"{++i}.ids"
					: $"{++i} - {specGroup.Name!.MakeSafeFileName()}.ids";
				var file = zipArchive.CreateEntry(name);
				using var str = file.Open();
				using XmlWriter writer = XmlWriter.Create(str, WriteSettings);
				ExportBuildingSmartIDS(specGroup, writer, logger);

			}
			return ExportedFormat.ZIP;
		}



		private static void ExportBuildingSmartIDS(SpecificationsGroup specGroup, XmlWriter xmlWriter, ILogger? logger)
		{
			xmlWriter.WriteStartElement("ids", "ids", @"http://standards.buildingsmart.org/IDS");
			// writer.WriteAttributeString("xsi", "xmlns", @"http://www.w3.org/2001/XMLSchema-instance");
			xmlWriter.WriteAttributeString("xmlns", "xs", null, "http://www.w3.org/2001/XMLSchema");
			xmlWriter.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
			xmlWriter.WriteAttributeString("xsi", "schemaLocation", null, "http://standards.buildingsmart.org/IDS http://standards.buildingsmart.org/IDS/1.0/ids.xsd");

			// info goes first
			WriteInfo(specGroup, xmlWriter);

			// then the specifications
			xmlWriter.WriteStartElement("specifications", IdsNamespace);
			foreach (var spec in specGroup.Specifications)
			{
				ExportBuildingSmartIDS(spec, xmlWriter, logger);
			}
			xmlWriter.WriteEndElement();

			xmlWriter.WriteEndElement();
			xmlWriter.Flush();
		}

		private static void WriteInfo(SpecificationsGroup specGroup, XmlWriter xmlWriter)
		{
			xmlWriter.WriteStartElement("info", IdsNamespace);
			// title needs to be written in any case
			xmlWriter.WriteElementString("title", IdsNamespace, specGroup.Name);
			// copy
			if (!string.IsNullOrEmpty(specGroup.Copyright))
				xmlWriter.WriteElementString("copyright", IdsNamespace, specGroup.Copyright);
			// version
			if (!string.IsNullOrEmpty(specGroup.Version))
				xmlWriter.WriteElementString("version", IdsNamespace, specGroup.Version);
			// description
			if (!string.IsNullOrEmpty(specGroup.Description))
				xmlWriter.WriteElementString("description", IdsNamespace, specGroup.Description);
			// author
			if (!string.IsNullOrEmpty(specGroup.Author))
				xmlWriter.WriteElementString("author", IdsNamespace, specGroup.Author);
			// date
			if (specGroup.Date.HasValue)
				xmlWriter.WriteElementString("date", IdsNamespace, $"{specGroup.Date.Value:yyyy-MM-dd}");
			// purpose
			if (!string.IsNullOrEmpty(specGroup.Purpose))
				xmlWriter.WriteElementString("purpose", IdsNamespace, specGroup.Purpose);
			// milestone
			if (!string.IsNullOrEmpty(specGroup.Milestone))
				xmlWriter.WriteElementString("milestone", IdsNamespace, specGroup.Milestone);

			xmlWriter.WriteEndElement();
		}

		private const string IdsNamespace = @"http://standards.buildingsmart.org/IDS";
		private const string IdsPrefix = "";

		private static readonly IdsLib.IfcSchema.IfcSchemaVersions defaultSchemaVersions =
				IdsLib.IfcSchema.IfcSchemaVersions.Ifc2x3 |
				IdsLib.IfcSchema.IfcSchemaVersions.Ifc4 |
				IdsLib.IfcSchema.IfcSchemaVersions.Ifc4x3;

		private static void ExportBuildingSmartIDS(Specification spec, XmlWriter xmlWriter, ILogger? logger)
		{
			xmlWriter.WriteStartElement("specification", IdsNamespace);
			var thisSpecVersion = defaultSchemaVersions;
			if (spec.IfcVersion is not null)
				thisSpecVersion = spec.IfcVersion.ToIds();
			// todo: add tostrin and fromstring to the idslib
			xmlWriter.WriteAttributeString("ifcVersion", thisSpecVersion.EncodeAsXmlSchemasAttribute());

			xmlWriter.WriteAttributeString("name", spec.Name ?? ""); // required
			if (spec.Description != null)
				xmlWriter.WriteAttributeString("description", spec.Description);
			// instructions
			if (spec.Instructions != null)
				xmlWriter.WriteAttributeString("instructions", spec.Instructions);
			// identifier
			if (spec.Guid != null)
				xmlWriter.WriteAttributeString("identifier", spec.Guid);



			// applicability
			xmlWriter.WriteStartElement("applicability", IdsNamespace);

			// applicability includes the cardinality
			if (spec.Cardinality is null)
				logger?.LogError("Cardinality is required for specification '{specname}' ({guid}).", spec.Name, spec.Guid);
			else
				spec.Cardinality.ExportBuildingSmartIDS(xmlWriter, logger);

			if (spec.Applicability is not null)
			{
				foreach (var item in spec.Applicability.Facets)
				{
					ExportBuildingSmartIDS(item, xmlWriter, false, logger, spec.Applicability, thisSpecVersion, null);
				}
			}
			xmlWriter.WriteEndElement();

			// requirements
			xmlWriter.WriteStartElement("requirements", IdsNamespace);
			if (spec.Requirement is not null)
			{
				var opts = spec.Requirement.RequirementOptions;
				for (int i = 0; i < spec.Requirement.Facets.Count; i++)
				{
					IFacet? facet = spec.Requirement.Facets[i];
					var option = GetProgressive(opts, i, facet, RequirementCardinalityOptions.DefaultCardinality);
					ExportBuildingSmartIDS(facet, xmlWriter, true, logger, spec.Requirement, thisSpecVersion, option);
				}
			}
			xmlWriter.WriteEndElement();

			xmlWriter.WriteEndElement();
		}

		static private RequirementCardinalityOptions GetProgressive(ObservableCollection<RequirementCardinalityOptions>? opts, int i, IFacet facet, RequirementCardinalityOptions.Cardinality defaultValue)
		{
			if (opts is null)
				return new RequirementCardinalityOptions(facet, defaultValue);
			if (i >= opts.Count)
				return new RequirementCardinalityOptions(facet, defaultValue);
			return opts[i];
		}

		private static void ExportBuildingSmartIDS(IFacet facet, XmlWriter xmlWriter, bool forRequirement, ILogger? logger, FacetGroup context, IfcSchemaVersions schemas, RequirementCardinalityOptions? requirementOption = null)
		{
			switch (facet)
			{
				case IfcTypeFacet tf:
					xmlWriter.WriteStartElement("entity", IdsNamespace);
					WriteFacetBaseAttributes(tf, xmlWriter, logger, forRequirement, requirementOption);
					if (tf.IncludeSubtypes && tf.IfcType is not null)
					{
						var values = tf.IfcType.AcceptedValues;
						string[] exactValues = [];
						IEnumerable<IValueConstraintComponent> complexConstraints = new List<IValueConstraintComponent>();
						if (values != null)
						{
							exactValues = values.OfType<ExactConstraint>().Select(x => x.Value).ToArray();
							complexConstraints = values.Except(values.OfType<ExactConstraint>());
						}
						if (exactValues.Any())
						{
							var classes = Enumerable.Empty<string>();
							foreach (var exact in exactValues)
							{
								classes = classes.Union(IdsLib.IfcSchema.SchemaInfo.GetConcreteClassesFrom(exact, schemas));
							}
							var subClasses = new ValueConstraint(classes);
							WriteConstraintValue(subClasses, xmlWriter, "name", logger, true);
							if (complexConstraints.Any())
							{
								logger?.LogWarning("Invalid EntityType - cannot contain combine exact and complex constraint types : {ifcType}", tf.IfcType);
							}
						}
						else
						{
							// It's a complex constraint types - just serialise without subclassess
							// TODO: More advanced expansion case - e.g. expand based on Pattern, Bounds etc.
							WriteConstraintValue(tf.IfcType, xmlWriter, "name", logger, true);
							logger?.LogWarning("TODO: ExportBuildingSmartIDS does not support SubType Expansion of complex constraints: {ifcType}", tf.IfcType);
						}
					}
					else
					{
						WriteConstraintValue(tf.IfcType, xmlWriter, "name", logger, true);
					}
					WriteConstraintValue(tf.PredefinedType, xmlWriter, "predefinedType", logger);
					xmlWriter.WriteEndElement();
					break;
				case IfcClassificationFacet cf:
					xmlWriter.WriteStartElement("classification", IdsNamespace);
					WriteFacetBaseAttributes(cf, xmlWriter, logger, forRequirement, requirementOption);
					WriteConstraintValue(cf.Identification, xmlWriter, "value", logger);
					WriteConstraintValue(cf.ClassificationSystem, xmlWriter, "system", logger);
					WriteFacetBaseElements(cf, xmlWriter); // from classification
					xmlWriter.WriteEndElement();
					break;
				case IfcPropertyFacet pf:
					xmlWriter.WriteStartElement("property", IdsNamespace);
					WriteFacetBaseAttributes(pf, xmlWriter, logger, forRequirement, requirementOption);
					if (!string.IsNullOrWhiteSpace(pf.DataType))
						xmlWriter.WriteAttributeString("dataType", pf.DataType.ToUpperInvariant());
					WriteConstraintValue(pf.PropertySetName, xmlWriter, "propertySet", logger);
					WriteConstraintValue(pf.PropertyName, xmlWriter, "baseName", logger);
					WriteConstraintValue(pf.PropertyValue, xmlWriter, "value", logger);
					WriteFacetBaseElements(pf, xmlWriter); // from Property
					xmlWriter.WriteEndElement();
					break;
				case MaterialFacet mf:
					xmlWriter.WriteStartElement("material", IdsNamespace);
					WriteFacetBaseAttributes(mf, xmlWriter, logger, forRequirement, requirementOption);
					WriteConstraintValue(mf.Value, xmlWriter, "value", logger);
					WriteFacetBaseElements(mf, xmlWriter); // from material
					xmlWriter.WriteEndElement();
					break;
				case AttributeFacet af:
					xmlWriter.WriteStartElement("attribute", IdsNamespace);
					WriteFacetBaseAttributes(af, xmlWriter, logger, forRequirement, requirementOption);
					WriteConstraintValue(af.AttributeName, xmlWriter, "name", logger);
					WriteConstraintValue(af.AttributeValue, xmlWriter, "value", logger);
					xmlWriter.WriteEndElement();
					break;
				case PartOfFacet pof:
					xmlWriter.WriteStartElement("partOf", IdsNamespace);
					WriteFacetBaseAttributes(pof, xmlWriter, logger, forRequirement, requirementOption);
					if (pof.GetRelation() != PartOfFacet.PartOfRelation.Undefined)
						xmlWriter.WriteAttributeString("relation", pof.GetRelation().ToString().ToUpperInvariant());
					if (pof.EntityType is not null)
					{
						// forRequirement is false, because the minMax Occurs does not apply at this level.
						ExportBuildingSmartIDS(pof.EntityType, xmlWriter, false, logger, context, schemas, null);
					}
					else
					{
						logger?.LogError("Invalid null EntityType in partOf facet for buildingSmart requirements.");
					}
					xmlWriter.WriteEndElement();
					break;
				default:
					logger?.LogWarning("TODO: ExportBuildingSmartIDS missing case for {type}.", facet.GetType());
					break;
			}
		}
		static private void LogDataLoss(ILogger? logger, FacetGroup context, IFacet facet, string propertyName, bool forRequirement)
		{
			logger?.LogError("Loss of data exporting group {grp}: property {prop} not available in {tp} for {ctx}.", context.Guid, propertyName, facet.GetType().Name, forRequirement ? "requirement" : "applicability");
		}

		static private void WriteSimpleValue(XmlWriter xmlWriter, string stringValue)
		{
			xmlWriter.WriteStartElement("simpleValue", IdsNamespace);
			xmlWriter.WriteString(stringValue);
			xmlWriter.WriteEndElement();
		}

		static private void WriteConstraintValue(ValueConstraint? value, XmlWriter xmlWriter, string name, ILogger? logger, bool forceUpperCase = false)
		{
			if (value == null)
				return;
			xmlWriter.WriteStartElement(name, IdsNamespace);
			if (value.IsSingleUndefinedExact(out string? exact))
			{
				if (exact is null)
				{
					logger?.LogError("Invalid null constraint found, added comment in exported file.");
					xmlWriter.WriteComment("Invalid null constraint found at this position"); // not sure this might even ever happen
				}
				else
				{
					if (forceUpperCase)
						WriteSimpleValue(xmlWriter, exact.ToUpperInvariant());
					else
						WriteSimpleValue(xmlWriter, exact);
				}
			}
			else if (value.AcceptedValues != null)
			{
				xmlWriter.WriteStartElement("restriction", @"http://www.w3.org/2001/XMLSchema");
				if (value.BaseType != NetTypeName.Undefined)
				{
					var val = ValueConstraint.GetXsdTypeString(value.BaseType);
					xmlWriter.WriteAttributeString("base", val);
				}
				foreach (var item in value.AcceptedValues)
				{
					if (item is PatternConstraint pc)
					{
						xmlWriter.WriteStartElement("pattern", @"http://www.w3.org/2001/XMLSchema");
						xmlWriter.WriteAttributeString("value", pc.Pattern);
						xmlWriter.WriteEndElement();
					}
					else if (item is ExactConstraint ec)
					{
						xmlWriter.WriteStartElement("enumeration", @"http://www.w3.org/2001/XMLSchema");
						if (forceUpperCase)
							xmlWriter.WriteAttributeString("value", ec.Value.ToString().ToUpperInvariant());
						else
							xmlWriter.WriteAttributeString("value", ec.Value.ToString());
						xmlWriter.WriteEndElement();
					}
					else if (item is RangeConstraint rc)
					{
						if (rc.MinValue != null)
						{
							var tp = rc.MinInclusive ? "minInclusive" : "minExclusive";
							xmlWriter.WriteStartElement(tp, @"http://www.w3.org/2001/XMLSchema");
							xmlWriter.WriteAttributeString("value", rc.MinValue.ToString());
							xmlWriter.WriteEndElement();
						}
						if (rc.MaxValue != null)
						{
							var tp = rc.MinInclusive ? "maxInclusive" : "maxExclusive";
							xmlWriter.WriteStartElement(tp, @"http://www.w3.org/2001/XMLSchema");
							xmlWriter.WriteAttributeString("value", rc.MaxValue.ToString());
							xmlWriter.WriteEndElement();
						}
					}
					else if (item is StructureConstraint sc)
					{
						if (sc.Length != null)
						{
							xmlWriter.WriteStartElement("length", @"http://www.w3.org/2001/XMLSchema");
							xmlWriter.WriteAttributeString("value", sc.Length.ToString());
							xmlWriter.WriteEndElement();
						}
						if (sc.MinLength != null)
						{
							xmlWriter.WriteStartElement("minLength", @"http://www.w3.org/2001/XMLSchema");
							xmlWriter.WriteAttributeString("value", sc.MinLength.ToString());
							xmlWriter.WriteEndElement();
						}
						if (sc.MaxLength != null)
						{
							xmlWriter.WriteStartElement("maxLength", @"http://www.w3.org/2001/XMLSchema");
							xmlWriter.WriteAttributeString("value", sc.MaxLength.ToString());
							xmlWriter.WriteEndElement();
						}
						if (sc.TotalDigits != null)
						{
							xmlWriter.WriteStartElement("totalDigits", @"http://www.w3.org/2001/XMLSchema");
							xmlWriter.WriteAttributeString("value", sc.TotalDigits.ToString());
							xmlWriter.WriteEndElement();
						}
						if (sc.FractionDigits != null)
						{
							xmlWriter.WriteStartElement("fractionDigits", @"http://www.w3.org/2001/XMLSchema");
							xmlWriter.WriteAttributeString("value", sc.FractionDigits.ToString());
							xmlWriter.WriteEndElement();
						}
					}
				}
				xmlWriter.WriteEndElement();
			}
			xmlWriter.WriteEndElement();
		}

		private static readonly string RequirementCardinalityAttributeName = "cardinality";

		static private void WriteFacetBaseAttributes(IFacet cf, XmlWriter xmlWriter, ILogger? logger, bool forRequirement, RequirementCardinalityOptions? option)
		{
			if (forRequirement)
			{
				option ??= new RequirementCardinalityOptions(cf, RequirementCardinalityOptions.DefaultCardinality);
				if (cf is IBuilsingSmartCardinality)
				{
					// use is required
					switch (option.RelatedFacetCardinality)
					{
						case RequirementCardinalityOptions.Cardinality.Prohibited:
							xmlWriter.WriteAttributeString(RequirementCardinalityAttributeName, "prohibited");
							break;
						case RequirementCardinalityOptions.Cardinality.Expected:
							xmlWriter.WriteAttributeString(RequirementCardinalityAttributeName, "required");
							break;
						case RequirementCardinalityOptions.Cardinality.Optional:
							xmlWriter.WriteAttributeString(RequirementCardinalityAttributeName, "optional");
							break;
						default:
							logger?.LogError("Invalid RequirementOption persistence for '{option}'", option);
							break;
					}
				}
				if (cf is FacetBase asFb)
					// instruction is optional
					if (!string.IsNullOrWhiteSpace(asFb.Instructions))
						xmlWriter.WriteAttributeString("instructions", asFb.Instructions);

			}

			if (
				cf is IfcPropertyFacet ||
				cf is IfcClassificationFacet ||
				cf is MaterialFacet
				)
			{
				if (cf is FacetBase cfb && !string.IsNullOrWhiteSpace(cfb.Uri))
					xmlWriter.WriteAttributeString("uri", cfb.Uri);
			}
		}

#pragma warning disable IDE0060 // Remove unused parameter
		static private void WriteFacetBaseElements(FacetBase cf, XmlWriter xmlWriter)
		{
			// function is kept in case it's gonna be useful again for structure purposes
		}
#pragma warning restore IDE0060 // Remove unused parameter

		/// <summary>
		/// Should use <see cref="LoadBuildingSmartIDS(Stream, ILogger?)"/> instead.
		/// </summary>
		[Obsolete("Use LoadBuildingSmartIDS instead.")]
		public static Xids? ImportBuildingSmartIDS(Stream stream, ILogger? logger = null)
		{
			return LoadBuildingSmartIDS(stream, logger);
		}


		/// <summary>
		/// Attempts to load an XIDS from a stream, where the stream is either an XML IDS or a zip file containing multiple IDS XML files
		/// </summary>
		/// <param name="stream">The XML or ZIP source stream to parse.</param>
		/// <param name="logger">The logger to send any errors and warnings to.</param>
		/// <returns>an XIDS or null if it could not be read.</returns>
		public static Xids? LoadBuildingSmartIDS(Stream stream, ILogger? logger = null)
		{
			if (IsZipped(stream))
			{
				using var zip = new ZipArchive(stream, ZipArchiveMode.Read, false);
				var xids = new Xids();
				foreach (var entry in zip.Entries)
				{
					try
					{
						if (entry.Name.EndsWith(".ids", StringComparison.InvariantCultureIgnoreCase))
						{
							using var idsStream = entry.Open();
							var element = XElement.Load(idsStream);
							LoadBuildingSmartIDS(element, logger, xids);
						}
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, "Failed to load IDS file from zip stream");
					}
				}
				if (!xids.AllSpecifications().Any())
				{
					logger?.LogWarning("No specifications found in this zip file. Ensure the zip contains *.ids files");
				}
				return xids;
			}
			else
			{
				var t = XElement.Load(stream);
				return LoadBuildingSmartIDS(t, logger);
			}
		}

		/// <summary>
		/// Should use <see cref="LoadBuildingSmartIDS(string, ILogger?)"/> instead.
		/// </summary>
		[Obsolete("Use LoadBuildingSmartIDS instead.")]
		public static Xids? ImportBuildingSmartIDS(string fileName, ILogger? logger = null)
		{
			return LoadBuildingSmartIDS(fileName, logger);
		}

		/// <summary>
		/// Attempts to unpersist an XIDS from the provider IDS XML file or zip file containing IDS files.
		/// </summary>
		/// <param name="fileName">File name of the Xids to load</param>
		/// <param name="logger">The logger to send any errors and warnings to.</param>
		/// <returns>an XIDS or null if it could not be read.</returns>
		public static Xids? LoadBuildingSmartIDS(string fileName, ILogger? logger = null)
		{
			if (!File.Exists(fileName))
			{
				var d = new DirectoryInfo(".");
				logger?.LogError("File '{fileName}' not found from executing directory '{fullDirectoryName}'", fileName, d.FullName);
				return null;
			}
			if (fileName.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
			{
				using var stream = File.OpenRead(fileName);
				if (IsZipped(stream))
				{
					return LoadBuildingSmartIDS(stream, logger);
				}
				else
				{
					logger?.LogError("Not a valid zip file");
					return null;
				}
			}
			else
			{
				try
				{
					var main = XElement.Parse(File.ReadAllText(fileName));
					return LoadBuildingSmartIDS(main, logger);
				}
				catch (Exception ex)
				{
					logger?.LogError(ex, "{ErrorMessage}", ex.Message);
					return null;
				}
			}
		}

		/// <summary>
		/// Should use <see cref="LoadBuildingSmartIDS(XElement, ILogger?, Xids?)"/> instead.
		/// </summary>
		[Obsolete("Use LoadBuildingSmartIDS instead.")]
		public static Xids? ImportBuildingSmartIDS(XElement main, ILogger? logger = null)
		{
			return LoadBuildingSmartIDS(main, logger);
		}

		/// <summary>
		/// Attempts to unpersist an XIDS from an XML element.
		/// </summary>
		/// <param name="main">the IDS element to load.</param>
		/// <param name="logger">the logging context</param>
		/// <param name="ids"></param>
		/// <returns>an entire new XIDS of null on errors</returns>
		public static Xids? LoadBuildingSmartIDS(XElement main, ILogger? logger = null, Xids? ids = null)
		{
			if (main.Name.LocalName == "ids")
			{
				var ret = ids ?? new Xids();
				var grp = new SpecificationsGroup(ret);
				ret.SpecificationsGroups.Add(grp);
				foreach (var sub in main.Elements())
				{
					var name = sub.Name.LocalName.ToLowerInvariant();
					if (name == "specifications")
					{
						AddSpecifications(grp, sub, logger);
					}
					else if (name == "info")
					{
						AddInfo(ret, grp, sub, logger);
					}
					else
					{
						LogUnexpected(sub, main, logger);
					}
				}
				return ret;
			}
			else
			{
				logger?.LogError("Unexpected element in ids: '{unexpectedName}'", main.Name.LocalName);
			}
			return null;
		}

		private static void AddInfo(Xids ret, SpecificationsGroup grp, XElement info, ILogger? logger)
		{
			if (ret is null)
				throw new ArgumentNullException(nameof(ret));

			foreach (var elem in info.Elements())
			{
				var name = elem.Name.LocalName.ToLowerInvariant();
				switch (name)
				{
					case "title":
						grp.Name = elem.Value;
						break;
					case "copyright":
						grp.Copyright = elem.Value;
						break;
					case "version":
						grp.Version = elem.Value;
						break;
					case "description":
						grp.Description = elem.Value;
						break;
					case "author":
						grp.Author = elem.Value;
						break;
					case "date":
						grp.Date = ReadDate(elem, logger);
						break;
					case "purpose":
						grp.Purpose = elem.Value;
						break;
					case "milestone":
						grp.Milestone = elem.Value;
						break;
					default:
						LogUnexpected(elem, info, logger);
						break;
				}
			}
		}

		internal static void LogUnexpected(XElement unepected, XElement parent, ILogger? logger)
		{
			logger?.LogWarning("Unexpected element '{unexpected}' in '{parentName}'.", unepected.Name.LocalName, parent.Name.LocalName);
		}

		internal static void LogUnexpected(XAttribute unepected, XElement parent, ILogger? logger)
		{
			logger?.LogWarning("Unexpected attribute '{unexpected}' in '{parentName}'.", unepected.Name.LocalName, parent.Name.LocalName);
		}

		internal static void LogUnexpectedValue(XAttribute unepected, XElement parent, ILogger? logger)
		{
			logger?.LogWarning("Unexpected value '{unexpValue}' for attribute '{unexpected}' in '{parentName}'.", unepected.Value, unepected.Name.LocalName, parent.Name.LocalName);
		}

		private static void LogUnsupportedOccurValue(XElement parent, ILogger? logger)
		{
			logger?.LogError("Unsupported values for cardinality in '{parentName}'. Defaulting to expected.", parent.Name.LocalName);
		}

		private static DateTime ReadDate(XElement elem, ILogger? logger)
		{
			try
			{
				var dt = XmlConvert.ToDateTime(elem.Value, XmlDateTimeSerializationMode.Unspecified);
				return dt;
			}
			catch (Exception ex)
			{
				logger?.LogError(ex, "Invalid value for date: {invalidDate}.", elem.Value);
				return DateTime.MinValue;
			}
		}

		private static void AddSpecifications(SpecificationsGroup destGroup, XElement specifications, ILogger? logger)
		{
			foreach (var elem in specifications.Elements())
			{
				var name = elem.Name.LocalName.ToLowerInvariant();
				switch (name)
				{
					case "specification":
						AddSpecification(destGroup, elem, logger);
						break;
					default:
						LogUnexpected(elem, specifications, logger);
						break;
				}
			}
		}

		private static void AddSpecification(SpecificationsGroup destGroup, XElement specificationElement, ILogger? logger)
		{
			var ret = new Specification(destGroup);
			var cardinality = new MinMaxCardinality();
			destGroup.Specifications.Add(ret);
			var schemaVersions = IdsLib.IfcSchema.IfcSchemaVersions.IfcNoVersion;


			foreach (var attribute in specificationElement.Attributes())
			{
				var attName = attribute.Name.LocalName.ToLower();
				switch (attName)
				{
					case "name":
						ret.Name = attribute.Value;
						break;
					case "description":
						ret.Description = attribute.Value;
						break;
					case "ifcversion":
						schemaVersions = attribute.Value.ParseXmlSchemasFromAttribute();
						ret.IfcVersion = ToXidsVersion(schemaVersions);
						break;
					case "instructions":
						ret.Instructions = attribute.Value;
						break;
					case "identifier":
						ret.Guid = attribute.Value;
						break;
					default:
						LogUnexpected(attribute, specificationElement, logger);
						break;
				}

			}
			foreach (var sub in specificationElement.Elements())
			{
				var name = sub.Name.LocalName.ToLowerInvariant();
				switch (name)
				{
					case "applicability":
						{
							cardinality.ReadAttributes(sub, logger);
							var fs = GetFacets(sub, logger, schemaVersions, out _);
							if (fs.Any())
								ret.SetFilters(fs);
							break;
						}
					case "requirements":
						{
							var fs = GetFacets(sub, logger, schemaVersions, out var options);
							if (fs.Any())
							{
								// we should ensure that options are populated.
								var tmp = new ObservableCollection<RequirementCardinalityOptions>();
								for (int i = 0; i < fs.Count;)
								{
									IFacet? facet = fs[i];
									if (facet != null)
									{
										var r = new RequirementCardinalityOptions(facet, RequirementCardinalityOptions.DefaultCardinality);
										if (i < options.Count && options[i] is not null)
										{
											r = options[i]!;
										}
										tmp.Add(r);
										i++;
									}
									else
										fs.RemoveAt(i);
								}
								ret.SetExpectations(fs);
								ret.Requirement!.RequirementOptions = tmp;
							}
							break;
						}
					default:
						LogUnexpected(sub, specificationElement, logger);
						break;
				}
			}
			ret.Cardinality = cardinality.Simplify();
		}

		private static List<IfcSchemaVersion> ToXidsVersion(IfcSchemaVersions tmp)
		{
			var ret = new List<IfcSchemaVersion>();
			if (tmp.HasFlag(IfcSchemaVersions.Ifc2x3))
				ret.Add(IfcSchemaVersion.IFC2X3);
			if (tmp.HasFlag(IfcSchemaVersions.Ifc4))
				ret.Add(IfcSchemaVersion.IFC4);
			if (tmp.HasFlag(IfcSchemaVersions.Ifc4x3))
				ret.Add(IfcSchemaVersion.IFC4X3);
			return ret;
		}

		private static IFacet GetMaterial(XElement elem, ILogger? logger, out RequirementCardinalityOptions opt)
		{
			MaterialFacet? ret = new(); // material is always initialized, because it's meaningful even if empty
			foreach (var sub in elem.Elements())
			{
				if (IsFacetBaseEntity(sub))
				{
					ret ??= new MaterialFacet();
					GetBaseEntity(sub, ret, logger);
				}
				else if (sub.Name.LocalName == "value")
				{
					ret ??= new MaterialFacet();
					ret.Value = GetConstraint(sub, logger);
				}
				else
				{
					LogUnexpected(sub, elem, logger);
				}
			}
			var minMax = new BsRequirementCardinality();
			foreach (var attribute in elem.Attributes())
			{
				if (IsBaseAttribute(attribute))
				{
					ret ??= new MaterialFacet();
					GetBaseAttribute(attribute, ret, logger);
				}
				else if (BsRequirementCardinality.IsRelevant(attribute, ref minMax))
				{
					// nothing to do, IsRelevant takes care of minMax
				}
				else
				{
					LogUnexpected(attribute, elem, logger);
				}
			}
			opt = new RequirementCardinalityOptions(
				ret,
				minMax.Evaluate(elem, logger)
				); // from material
			return ret;
		}

		private static IFacet? GetPartOf(XElement elem, IfcSchemaVersions schemaVersions, ILogger? logger, out RequirementCardinalityOptions? opt)
		{
			PartOfFacet? ret = null;
			foreach (var sub in elem.Elements())
			{
				var locName = sub.Name.LocalName.ToLowerInvariant();
				switch (locName)
				{
					case "entity":
						var t = GetEntity(sub, schemaVersions, logger);
						if (t is IfcTypeFacet fct)
						{
							ret ??= new PartOfFacet();
							ret.EntityType = fct;
						}
						break;
					default:
						LogUnexpected(sub, elem, logger);
						break;
				}
			}
			var minMax = new BsRequirementCardinality();
			foreach (var attribute in elem.Attributes())
			{

				if (IsBaseAttribute(attribute))
				{
					ret ??= new PartOfFacet();
					GetBaseAttribute(attribute, ret, logger);
				}
				else if (BsRequirementCardinality.IsRelevant(attribute, ref minMax))
				{
					// nothing to do, IsRelevant takes care of minMax
				}
				else
				{
					var locName = attribute.Name.LocalName.ToLowerInvariant();
					switch (locName)
					{
						case "relation":
							ret ??= new PartOfFacet();
							ret.EntityRelation = attribute.Value;
							break;
						default:
							LogUnexpected(attribute, elem, logger);
							break;
					}
				}
			}
			if (ret is not null)
				opt = new RequirementCardinalityOptions(ret, minMax.Evaluate(elem, logger)); // from partOf            
			else
				opt = null;
			return ret;
		}

		private static IFacet? GetProperty(XElement elem, ILogger? logger, out RequirementCardinalityOptions? opt)
		{
			IfcPropertyFacet? ret = null;
			foreach (var sub in elem.Elements())
			{
				if (IsFacetBaseEntity(sub))
				{
					ret ??= new IfcPropertyFacet();
					GetBaseEntity(sub, ret, logger);
					continue;
				}
				var locName = sub.Name.LocalName.ToLowerInvariant();
				switch (locName)
				{
					case "propertyset":
						ret ??= new IfcPropertyFacet();
						ret.PropertySetName = GetConstraint(sub, logger);
						break;
					case "basename": // either property or name is redundant
						ret ??= new IfcPropertyFacet();
						ret.PropertyName = GetConstraint(sub, logger);
						break;
					case "value":
						ret ??= new IfcPropertyFacet();
						ret.PropertyValue = GetConstraint(sub, logger);
						break;
					default:
						LogUnexpected(sub, elem, logger);
						break;
				}
			}
			var minMax = new BsRequirementCardinality();
			foreach (var attribute in elem.Attributes())
			{
				if (IsBaseAttribute(attribute))
				{
					ret ??= new IfcPropertyFacet();
					GetBaseAttribute(attribute, ret, logger);
				}
				else if (attribute.Name.LocalName == "dataType")
				{
					ret ??= new IfcPropertyFacet();
					ret.DataType = attribute.Value;
				}
				else if (BsRequirementCardinality.IsRelevant(attribute, ref minMax))
				{
					// nothing to do, IsRelevant takes care of minMax
				}
				else
				{
					LogUnexpected(attribute, elem, logger);
				}
			}
			if (ret is not null)
				opt = new RequirementCardinalityOptions(ret, minMax.Evaluate(elem, logger)); // from property
			else
				opt = null;

			return ret;
		}

		private static ValueConstraint? GetConstraint(XElement elem, ILogger? logger)
		{
			XNamespace ns = "http://www.w3.org/2001/XMLSchema";
			var restriction = elem.Element(ns + "restriction");
			if (restriction == null)
			{
				// get the textual content as a fixed string value
				// this is the case of <simpleValue>xxx</simpleValue>
				var content = elem.Value;
				var tc = ValueConstraint.SingleUndefinedExact(content);
				return tc;
			}
			NetTypeName t = NetTypeName.Undefined;
			var bse = restriction.Attribute("base");
			if (bse != null && bse.Value != null)
			{
				var tval = bse.Value;
				t = ValueConstraint.GetNamedTypeFromXsd(tval);
			}

			// we prepare the different possible scenarios, but then check in the end that the 
			// xml encountered is solid.
			//
			List<string>? enumeration = null;
			List<PatternConstraint>? patternc = null;
			RangeConstraint? range = null;
			StructureConstraint? structure = null;

			foreach (var sub in restriction.Elements())
			{
				if (sub.Name.LocalName == "enumeration")
				{
					var val = sub.Attribute("value");
					if (val != null)
					{
						enumeration ??= new List<string>();
						enumeration.Add(val.Value);
					}
				}
				else if (
					sub.Name.LocalName == "minInclusive"
					||
					sub.Name.LocalName == "minExclusive"
					)
				{
					var val = sub.Attribute("value")?.Value;
					if (val != null)
					{
						range ??= new RangeConstraint();
						range.MinValue = val;
						range.MinInclusive = sub.Name.LocalName == "minInclusive";
					}
				}
				else if (
					sub.Name.LocalName == "maxInclusive"
					||
					sub.Name.LocalName == "maxExclusive"
					)
				{
					var val = sub.Attribute("value")?.Value;
					if (val != null)
					{
						range ??= new RangeConstraint();
						range.MaxValue = val;
						range.MaxInclusive = sub.Name.LocalName == "maxInclusive";
					}
				}
				else if (sub.Name.LocalName == "pattern")
				{
					var val = sub.Attribute("value");
					if (val != null)
					{
						patternc ??= new List<PatternConstraint>();
						patternc.Add(new PatternConstraint() { Pattern = val.Value });
					}
				}
				else if (sub.Name.LocalName == "minLength")
				{
					var val = sub.Attribute("value");
					if (val != null && int.TryParse(val.Value, out var ival))
					{
						structure ??= new StructureConstraint();
						structure.MinLength = ival;
					}
				}
				else if (sub.Name.LocalName == "maxLength")
				{
					var val = sub.Attribute("value");
					if (val != null && int.TryParse(val.Value, out var ival))
					{
						structure ??= new StructureConstraint();
						structure.MaxLength = ival;
					}
				}
				else if (sub.Name.LocalName == "length")
				{
					var val = sub.Attribute("value");
					if (val != null && int.TryParse(val.Value, out var ival))
					{
						structure ??= new StructureConstraint();
						structure.Length = ival;
					}
				}
				else if (sub.Name.LocalName == "totalDigits")
				{
					var val = sub.Attribute("value");
					if (val != null && int.TryParse(val.Value, out var ival))
					{
						structure ??= new StructureConstraint();
						structure.TotalDigits = ival;
					}
				}
				else if (sub.Name.LocalName == "fractionDigits")
				{
					var val = sub.Attribute("value");
					if (val != null && int.TryParse(val.Value, out var ival))
					{
						structure ??= new StructureConstraint();
						structure.FractionDigits = ival;
					}
				}
				else if (sub.Name.LocalName == "annotation") // todo: IDSTALK: complexity of annotation
				{
					// is the implementation of xs:annotation a big overkill for the app?
					// see  https://www.w3schools.com/xml/el_appinfo.asp
					//      https://www.w3schools.com/xml/el_annotation.asp
					//      xs:documentation also has any xml in the body... complicated.

				}
				else
				{
					LogUnexpected(sub, elem, logger);
				}
			}
			// check that the temporary variable are coherent with valid value
			var count = (enumeration != null) ? 1 : 0;
			count += (range != null) ? 1 : 0;
			count += (patternc != null) ? 1 : 0;
			count += (structure != null) ? 1 : 0;
			if (count == 0)
			{
				logger?.LogWarning("Invalid value constraint for {localname} full xml '{elem}'.", elem.Name.LocalName, elem);
				return null;
			}
			// initialize return value
			var ret = new ValueConstraint(t)
			{
				AcceptedValues = new List<IValueConstraintComponent>()
			};
			// populate the constraints
			if (enumeration != null)
			{
				ret.AcceptedValues.AddRange(enumeration.Select(x => new ExactConstraint(x)));
			}
			if (range != null)
			{
				ret.AcceptedValues.Add(range);
			}
			if (patternc != null)
			{
				ret.AcceptedValues.AddRange(patternc);
			}
			if (structure != null)
			{
				ret.AcceptedValues.Add(structure);
			}
			return ret;
		}

		private static List<IFacet> GetFacets(XElement elem, ILogger? logger, IfcSchemaVersions schemaVersions, out IList<RequirementCardinalityOptions?> options)
		{
			var fs = new List<IFacet>();
			var opts = new List<RequirementCardinalityOptions?>();
			foreach (var sub in elem.Elements())
			{
				IFacet? tempFacet = null;
				RequirementCardinalityOptions? tempOption = null;
				var locName = sub.Name.LocalName.ToLowerInvariant();
				switch (locName)
				{
					case "entity":
						tempFacet = GetEntity(sub, schemaVersions, logger);
						break;
					case "classification":
						tempFacet = GetClassification(sub, logger, out tempOption);
						break;
					case "property":
						tempFacet = GetProperty(sub, logger, out tempOption);
						break;
					case "material":
						tempFacet = GetMaterial(sub, logger, out tempOption);
						break;
					case "attribute":
						tempFacet = GetAttribute(sub, logger, out tempOption);
						break;
					case "partof":
						tempFacet = GetPartOf(sub, schemaVersions, logger, out tempOption);
						break;
					default:
						LogUnexpected(sub, elem, logger);
						break;
				}
				if (tempFacet != null)
				{
					fs.Add(tempFacet);
					opts.Add(tempOption);
				}
			}
			options = opts;
			return fs;
		}

		private static IFacet? GetAttribute(XElement elem, ILogger? logger, out RequirementCardinalityOptions? opt)
		{
			AttributeFacet? ret = null;
			foreach (var sub in elem.Elements())
			{
				var subname = sub.Name.LocalName.ToLowerInvariant();
				switch (subname)
				{
					case "name":
						ret ??= new AttributeFacet();
						ret.AttributeName = GetConstraint(sub, logger);
						break;
					case "value":
						ret ??= new AttributeFacet();
						ret.AttributeValue = GetConstraint(sub, logger);
						break;
					default:
						LogUnexpected(sub, elem, logger);
						break;
				}
			}
			var minMax = new BsRequirementCardinality();
			foreach (var attribute in elem.Attributes())
			{
				var subName = attribute.Name.LocalName.ToLowerInvariant();
				if (IsBaseAttribute(attribute))
				{
					ret ??= new AttributeFacet();
					GetBaseAttribute(attribute, ret, logger);
				}
				else if (BsRequirementCardinality.IsRelevant(attribute, ref minMax))
				{
					// nothing to do, IsRelevant takes care of minMax
				}
				else
				{
					LogUnexpected(attribute, elem, logger);
				}
			}
			if (ret is not null)
				opt = new RequirementCardinalityOptions(ret, minMax.Evaluate(elem, logger)); // from attribute
			else
				opt = null;

			return ret;
		}

		private class BsRequirementCardinality
		{
			public string Card { get; set; } = "";

			internal static bool IsRelevant(XAttribute attribute, ref BsRequirementCardinality minMax)
			{
				if (attribute.Name == RequirementCardinalityAttributeName)
				{
					minMax.Card = attribute.Value;
					return true;
				}
				return false;
			}

			private static readonly RequirementCardinalityOptions.Cardinality DefaultCardinality = RequirementCardinalityOptions.Cardinality.Expected;

			internal RequirementCardinalityOptions.Cardinality Evaluate(XElement elem, ILogger? logger)
			{
				if (Card == "")
					return DefaultCardinality; // set default
				else if (Card == "prohibited")
					return RequirementCardinalityOptions.Cardinality.Prohibited;
				else if (Card == "optional")
					return RequirementCardinalityOptions.Cardinality.Optional;
				else if (Card == "required")
					return RequirementCardinalityOptions.Cardinality.Expected;
				// throw warning and set default value
				LogUnsupportedOccurValue(elem, logger);
				return DefaultCardinality; // set default
			}
		}

		private static IFacet GetClassification(XElement elem, ILogger? logger, out RequirementCardinalityOptions opt)
		{
			IfcClassificationFacet? ret = new(); // classification is always initialized, because it's meaningful even if empty
			foreach (var sub in elem.Elements())
			{
				if (IsFacetBaseEntity(sub))
				{
					ret ??= new IfcClassificationFacet();
					GetBaseEntity(sub, ret, logger);
				}
				else if (sub.Name.LocalName == "system")
				{
					ret ??= new IfcClassificationFacet();
					ret.ClassificationSystem = GetConstraint(sub, logger);
				}
				else if (sub.Name.LocalName == "value")
				{
					ret ??= new IfcClassificationFacet();
					ret.Identification = GetConstraint(sub, logger);
				}
				else
				{
					LogUnexpected(sub, elem, logger);
				}
			}

			var minMax = new BsRequirementCardinality();
			foreach (var attribute in elem.Attributes())
			{
				var locAtt = attribute.Name.LocalName;
				if (IsBaseAttribute(attribute))
				{
					ret ??= new IfcClassificationFacet();
					GetBaseAttribute(attribute, ret, logger);
				}
				else if (BsRequirementCardinality.IsRelevant(attribute, ref minMax))
				{
					// nothing to do, IsRelevant takes care of minMax
				}
				else
				{
					LogUnexpected(attribute, elem, logger);
				}
			}
			opt = new RequirementCardinalityOptions(ret, minMax.Evaluate(elem, logger)); // from classification
			return ret;
		}

#pragma warning disable IDE0060 // Remove unused parameter
		private static void GetBaseEntity(XElement sub, FacetBase ret, ILogger? logger)
		{
			var local = sub.Name.LocalName.ToLowerInvariant();
			//if (local == "instructions")
			//    ret.Instructions = sub.Value;
			//else
			logger?.LogWarning("Unexpected element {local} reading FacetBase.", local);
		}
#pragma warning restore IDE0060 // Remove unused parameter

#pragma warning disable IDE0060 // Remove unused parameter (sub)
		private static bool IsFacetBaseEntity(XElement sub)
		{
			//switch (sub.Name.LocalName)
			//{
			//    case "instructions":
			//        return true;
			//    default:
			//        return false;
			//}
			return false;
		}
#pragma warning restore IDE0060 // Remove unused parameter

		private static bool IsBaseAttribute(XAttribute attribute)
		{
			return attribute.Name.LocalName switch
			{
				"uri" or "instructions" => true,
				_ => false,
			};
		}

		private static void GetBaseAttribute(XAttribute attribute, FacetBase ret, ILogger? logger)
		{
			if (attribute.Name.LocalName == "uri")
				ret.Uri = attribute.Value;
			else if (attribute.Name.LocalName == "instructions")
				ret.Instructions = attribute.Value;
			else
			{
				logger?.LogError("Unrecognised base attribute {attributeName}", attribute.Name);
			}
		}


		private const bool defaultSubTypeInclusion = false;

		private static IFacet? GetEntity(XElement elem, IfcSchemaVersions schemaVersions, ILogger? logger)
		{
			IfcTypeFacet? ret = null;
			foreach (var sub in elem.Elements())
			{
				var locName = sub.Name.LocalName.ToLowerInvariant();
				switch (locName)
				{
					case "name":
						ret ??= new IfcTypeFacet() { IncludeSubtypes = defaultSubTypeInclusion };
						ret.IfcType = GetConstraint(sub, logger);
						break;
					case "predefinedtype":
						ret ??= new IfcTypeFacet() { IncludeSubtypes = defaultSubTypeInclusion };
						ret.PredefinedType = GetConstraint(sub, logger);
						break;
					default:
						LogUnexpected(sub, elem, logger);
						break;
				}
			}
			if (ret is not null && ret.IfcType is not null && TryOptimizeTypeConstraint(ret.IfcType, schemaVersions, out var type, out bool includeSubtypes))
			{
				ret.IfcType = type; // directly assigning a string is persisted more concisely
				ret.IncludeSubtypes = includeSubtypes;
			}
			foreach (var attribute in elem.Attributes())
			{
				if (IsBaseAttribute(attribute))
				{
					ret ??= new IfcTypeFacet() { IncludeSubtypes = defaultSubTypeInclusion };
					GetBaseAttribute(attribute, ret, logger);
				}
				else
					LogUnexpected(attribute, elem, logger);
			}
			return ret;
		}

		private static bool TryOptimizeTypeConstraint(ValueConstraint ifcType, IfcSchemaVersions schemaVersions, [NotNullWhen(true)] out string? type, out bool includeSubtypes)
		{
			if (ifcType.HasAnyAcceptedValue())
			{
				var exacts = ifcType.AcceptedValues.OfType<ExactConstraint>().Select(x => x.Value).ToArray();
				if (exacts.Length > 1 && exacts.Length == ifcType.AcceptedValues.Count)
				{
					if (SchemaInfo.TrySearchTopClass(exacts, schemaVersions, out var top))
					{
						type = top;
						includeSubtypes = true;
						return true;
					}
				}
			}
			includeSubtypes = false;
			type = null;
			return false;
		}

		private static string GetFirstString(XElement sub)
		{
			if (!string.IsNullOrEmpty(sub.Value))
				return sub.Value;
			var nm = sub.Name.LocalName.ToLowerInvariant();
			switch (nm)
			{
				case "pattern":
					{
						var val = sub.Attribute("value");
						if (!string.IsNullOrEmpty(val?.Value))
						{
							return val!.Value; // bang is redundant in net5, but net2 is capricious with nullability checks
						}
						break;
					}
			}
			foreach (var sub2 in sub.Elements())
			{
				var subS = GetFirstString(sub2); // recursive
				if (!string.IsNullOrEmpty(subS))
					return subS;
			}
			return "";
		}
	}
}
