﻿using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Xbim.InformationSpecifications.Cardinality
{
	/// <summary>
	/// A way of defining <see cref="ICardinality"/> by Min and Max expected values.
	/// </summary>
	public class MinMaxCardinality : ICardinality
	{
		/// <summary>
		/// Default constructor, Min = 1, Max = unbounded (Required)
		/// </summary>
		public MinMaxCardinality()
		{
		}

		/// <summary>
		/// Specific value constructor
		/// </summary>
		public MinMaxCardinality(int Minimum, int? Maximum = null)
		{
			MinOccurs = Minimum;
			MaxOccurs = Maximum;
		}

		/// <summary>
		/// The minimum cardinality.
		/// Defaults to 1 (required). This is line with XSD defaults
		/// </summary>
		public int MinOccurs { get; set; } = 1;

		/// <summary>
		/// The maximum expected cardinality.
		/// If null to be considered unbounded, which is also the default
		/// </summary>
		public int? MaxOccurs { get; set; } = null;

		/// <summary>
		/// Requirement needed if minOccur is set to 0, See <see cref="ICardinality.ExpectsRequirements"/>.
		/// </summary>
		public bool ExpectsRequirements => MinOccurs == 0;

		/// <summary>
		/// No requirement allowed if maxoccurs is set to 0, See <see cref="ICardinality.AllowsRequirements"/>.
		/// </summary>
		public bool AllowsRequirements => !(MaxOccurs.HasValue && MaxOccurs.Value == 0);

		/// <inheritdoc />
		public string Description
		{
			get
			{
				var smp = Simplify();
				if (smp is SimpleCardinality smp2)
				{
					return smp2.Description;
				}
				var sb = new StringBuilder();
				sb.Append(MinOccurs);
				if (MaxOccurs.HasValue)
					sb.Append($"..{MaxOccurs}");
				return sb.ToString();
			}
		}

		/// <inheritdoc />
		public bool IsModelConstraint
		{
			get
			{
				if (MinOccurs != 0)
					return true;
				if (MaxOccurs.HasValue)
					return true;
				return false;
			}
		}

		/// <inheritdoc />
		public void ExportBuildingSmartIDS(XmlWriter xmlWriter, ILogger? logger)
		{
			xmlWriter.WriteAttributeString("minOccurs", MinOccurs.ToString());
			if (MaxOccurs != null)
				xmlWriter.WriteAttributeString("maxOccurs", MaxOccurs.ToString());
		}

		/// <summary>
		/// Attempt to reduce this to a SimpleCardinality, or return self.
		/// </summary>
		/// <returns>An instance of SimpleCardinality, if possible, otherwise this instance of MinMaxCardinality.</returns>
		public ICardinality Simplify()
		{
			if (MinOccurs == 0 && MaxOccurs is null)
				return new SimpleCardinality() { ApplicabilityCardinality = CardinalityEnum.Optional };
			else if (MinOccurs == 1 && MaxOccurs is null)
				return new SimpleCardinality() { ApplicabilityCardinality = CardinalityEnum.Required };
			else if (MinOccurs == 0 && MaxOccurs is not null && MaxOccurs.Value == 0)
				return new SimpleCardinality() { ApplicabilityCardinality = CardinalityEnum.Prohibited };
			return this;
		}

		/// <inheritdoc />
		public bool IsValid()
		{
			if (!MaxOccurs.HasValue)
				return true;
			return MaxOccurs >= MinOccurs;
		}

		/// <inheritdoc />
		public bool IsSatisfiedBy(int count)
		{
			if (!MaxOccurs.HasValue)
				return count >= MinOccurs;
			return
				count >= MinOccurs
				&&
				count <= MaxOccurs.Value;
		}

		internal void ReadAttributes(XElement sub, ILogger? logger)
		{

			foreach (var attribute in sub.Attributes())
			{
				switch (attribute.Name.LocalName.ToLowerInvariant())
				{
					case "minoccurs":
						if (int.TryParse(attribute.Value, out int tmpMin))
							MinOccurs = tmpMin;
						else
							Xids.LogUnexpectedValue(attribute, sub, logger);
						break;
					case "maxoccurs":
						if (attribute.Value == "unbounded")
							MaxOccurs = null; // null is considered to mean unbounded
						else if (int.TryParse(attribute.Value, out int tmpMax))
							MaxOccurs = tmpMax;
						else
							Xids.LogUnexpectedValue(attribute, sub, logger);
						break;
				}
			}

		}

		/// <inheritdoc />
		public bool NoMatchingEntities => MaxOccurs.HasValue && MaxOccurs.Value == 0;
	}
}
