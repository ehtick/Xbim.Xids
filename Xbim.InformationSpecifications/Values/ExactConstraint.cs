﻿using Microsoft.Extensions.Logging;
using System;

namespace Xbim.InformationSpecifications
{
	public class ExactConstraint : IValueConstraint, IEquatable<ExactConstraint>
	{
		public ExactConstraint(string value)
		{
			Value = value;
		}

		public string Value { get; set; }

		public bool IsSatisfiedBy(object candiatateValue, ValueConstraint context, ILogger logger = null)
		{
			return Value.Equals(candiatateValue.ToString());
		}

		public override string ToString()
		{
			if (Value != null)
				return Value;
			return "<null>";
		}

		public override int GetHashCode()
		{
			if (Value != null)
				return Value.GetHashCode();
			return base.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			return Equals(obj as ExactConstraint);
		}

		public bool Equals(ExactConstraint other)
		{
			if (other == null)
				return false;
			// using tuple's trick to evaluate equality
			return (Value, true).Equals((other.Value, true));
		}

		public string Short()
		{
			return Value;
		}
	}
}
