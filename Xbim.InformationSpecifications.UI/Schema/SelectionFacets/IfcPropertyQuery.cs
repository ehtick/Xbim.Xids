using System;

namespace Xbim.IDS
{
    public enum IfcPropertyQueryPropertyFormat
    {
        text,
        integer,
        floating,
        boolean,
    }


    public partial class IfcPropertyQuery : IFilter, IEquatable<IfcPropertyQuery>
    {
		public string PropertySetName { get; set; }

		public string PropertyName { get; set; }

		public string PropertyValue { get; set; }

		public IfcPropertyQueryPropertyFormat PropertyFormat { get; set; }

        public string Short()
        {
            return ToString();
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as IfcPropertyQuery);
        }

        public override int GetHashCode()
        {
            return $"{PropertySetName}{PropertyName}{PropertyValue}{PropertyFormat}".GetHashCode();
        }

        public bool Equals(IfcPropertyQuery other)
        {
            if (other == null)
                return false;

            if (PropertySetName.ToLowerInvariant() != other.PropertySetName.ToLowerInvariant())
                return false;
            if (PropertyName.ToLowerInvariant() != other.PropertyName.ToLowerInvariant())
                return false;
            if (PropertyValue.ToLowerInvariant() != other.PropertyValue.ToLowerInvariant())
                return false;
            if (PropertyFormat != other.PropertyFormat)
                return false;
            return true;
        }
    }
}