﻿using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using Newtonsoft.Json;

namespace DivinityModManager.Models
{
	public struct DivinityLoadOrderEntry
	{
		public string UUID { get; set; }
		public string Name { get; set; }

		public DivinityLoadOrderEntry Clone()
		{
			return new DivinityLoadOrderEntry() { Name = this.Name, UUID = this.UUID };
		}
	}

	[JsonObject(MemberSerialization.OptIn)]
	public class DivinityLoadOrder : ReactiveObject, IActivatable
	{
		private string name;

		[JsonProperty]
		public string Name
		{
			get => name;
			set { this.RaiseAndSetIfChanged(ref name, value); }
		}

		/*
		private string displayName;
		public string DisplayName
		{
			get => String.IsNullOrEmpty(displayName) ? name : displayName;
			set { this.RaiseAndSetIfChanged(ref displayName, value); }
		}
		*/

		private DateTime lastModifiedDate;

		public DateTime LastModifiedDate
		{
			get => lastModifiedDate;
			set {
				this.RaiseAndSetIfChanged(ref lastModifiedDate, value);
				LastModified = lastModifiedDate.ToString("g");
			}
		}

		private string lastModified;

		public string LastModified
		{
			get => lastModified;
			set { this.RaiseAndSetIfChanged(ref lastModified, value); }
		}


		[JsonProperty]
		public List<DivinityLoadOrderEntry> Order { get; set; } = new List<DivinityLoadOrderEntry>();

		public void SetOrder(IEnumerable<DivinityLoadOrderEntry> nextOrder)
		{
			Order.Clear();
			Order.AddRange(nextOrder);
		}

		public DivinityLoadOrder Clone()
		{
			return new DivinityLoadOrder()
			{
				Name = this.name,
				Order = this.Order.ToList(),
				LastModifiedDate = this.LastModifiedDate
			};
		}

		public IDisposable ActiveModBinding { get; set; }

		public void CreateActiveOrderBind(IObservable<IChangeSet<DivinityModData>> changeSet)
		{
			/*
			ActiveModBinding = changeSet.AutoRefresh(m => m.Index).Transform(m => new DivinityLoadOrderEntry { Name = m.Name, UUID = m.UUID }).Buffer(TimeSpan.FromMilliseconds(250)).
					FlattenBufferResult().Bind(Order).
					Subscribe(c =>
					{
						//newOrder.Order = c.ToList();

						Trace.WriteLine($"Load order {Name} changed.");
						Trace.WriteLine("=========================");
						Trace.WriteLine($"{String.Join(Environment.NewLine + "	", Order.Select(e => e.Name))}");
						Trace.WriteLine("=========================");
					});
			*/
		}

		public void DisposeBinding()
		{
			if(ActiveModBinding != null)
			{
				//savedList = new List<DivinityLoadOrderEntry>(Order);
				ActiveModBinding.Dispose();
			}
		}
	}
}
