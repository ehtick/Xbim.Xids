using System;

namespace Xbim.IDS
{
    public partial class HasDocumentReference : ExpectationFacet, IEquatable<HasDocumentReference>
	{
		public string DocumentName { get; set; }

		public string DocumentStatus { get; set; }

		public string RequiredAttributes { get; set; }

		public bool Equals(HasDocumentReference other)
		{
			if (other == null)
				return false;
			if (DocumentName.ToLowerInvariant() != other.DocumentName.ToLowerInvariant())
				return false;
			if (DocumentStatus.ToLowerInvariant() != other.DocumentStatus.ToLowerInvariant())
				return false;
			if (RequiredAttributes.ToLowerInvariant() != other.RequiredAttributes.ToLowerInvariant())
				return false;
			return base.Equals(other as ExpectationFacet);
		}
		public override bool Equals(object obj)
		{
			return this.Equals(obj as HasDocumentReference);
		}

		public override int GetHashCode()
		{
			return $"{DocumentName}-{DocumentStatus}-{RequiredAttributes}".GetHashCode();
		}

		public override bool Validate()
		{
			// Strictly speaking we only need DocumentName
			if (string.IsNullOrWhiteSpace(DocumentName))
				return false;
			//if (Guid == Guid.Empty)
			//	Guid = Guid.NewGuid();
			return true;
		}
	}
}