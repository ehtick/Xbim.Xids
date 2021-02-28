using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Xbim.IDS
{
	public partial class ModelPart 
    {
		public ModelPart()
		{ }

		public ModelPart(Ids ids)
		{
			ids.ModelSetRepository.Add(this);
			Guid = System.Guid.NewGuid().ToString();
		}

		public ObservableCollection<IFilter> Items { get; set; } = new ObservableCollection<IFilter>();

		public string Name { get; set; }

		public string Reference { get; set; }

		public string Guid { get; set; }

		public string Description { get; set; }

		public string Short()
		{
			if (!string.IsNullOrWhiteSpace(Name))
				return $"{Name} ({Items.Count})";
			if (Items.Any())
			{
				return string.Join(" and ", Items.Select(x => x.Short()));
			}
			if (!string.IsNullOrWhiteSpace(Description))
				return Description;
			return "<undefined>";
		}
	}
}