using System;

namespace Sandbox.UI.Tests
{

	/// <summary>
	/// Scroll panel that creates its contents as they become visible
	///
	/// TODO: we need to let panels know, or recreate them, when Data changes
	///
	/// </summary>
	[Library( "virtualscrollpanel" )]
	public class VirtualScrollPanel : Panel
	{
		private Dictionary<int, object> CellData = new Dictionary<int, object>();

		public virtual List<object> Data { get; set; } = new List<object>();

		public Dictionary<int, Panel> CreatedPanels = new Dictionary<int, Panel>();
		List<int> RemoveQueue = new List<int>();

		// TODO - general layout base class so this is swappable
		public GridLayout Layout = new GridLayout();

		public bool NeedsRebuild { get; set; }


		public virtual void AddItem( object item )
		{
			Data.Add( item );
			NeedsRebuild = true;
		}

		public void AddItems( object[] items )
		{
			Data.AddRange( items );
			NeedsRebuild = true;
		}

		public virtual void Clear()
		{
			Data.Clear();
			NeedsRebuild = true;

			foreach( var p in CreatedPanels )
			{
				p.Value.Delete( true );
			}

			CreatedPanels.Clear();
		}

		public override void Tick()
		{
			base.Tick();

			if ( ComputedStyle == null ) return;
			if ( !IsVisible ) return;

			if ( Layout.Update( Box, ScaleFromScreen, ScrollOffset.y * ScaleFromScreen, ComputedStyle.JustifyContent.Value ) || NeedsRebuild )
			{
				NeedsRebuild = false;
				Layout.GetVisibleRange( out var firstVisibleIndex, out var lastVisibleIndex );

				DeleteNotVisible( firstVisibleIndex, lastVisibleIndex );

				for ( int i = firstVisibleIndex; i < lastVisibleIndex; i++ )
				{
					RefreshCreated( i );
				}

			}
		}

		public void SetItems( IEnumerable<object> enumerable )
		{
			Data.Clear();
			Data.AddRange( enumerable );
			NeedsRebuild = true;
		}

		void DeleteNotVisible( int min, int max )
		{
			RemoveQueue.Clear();
			RemoveQueue.AddRange( CreatedPanels.Keys.Where( x => x < min || x > max || !HasData( x ) ) );

			for ( int i = 0; i < RemoveQueue.Count; i++ )
			{
				var idx = RemoveQueue[i];
				if ( CreatedPanels.Remove( idx, out var panel ) )
				{
					panel.Delete( true );
				}
			}
			RemoveQueue.Clear();
		}

		/// <summary>
		/// Return true if we have this data slot
		/// </summary>
		public bool HasData( int i ) => i < Data.Count && i >= 0;

		public void RefreshCreated( int i )
		{
			if ( Data.Count <= i || i < 0 ) return;

			bool needsRebuild = true;
			if ( CellData.TryGetValue( i, out var lastData ) )
			{
				needsRebuild = lastData != Data[i];
			}

			if ( !CreatedPanels.TryGetValue( i, out var panel ) || needsRebuild )
			{
				if ( needsRebuild )
				{
					panel?.Delete();
				}

				panel = Add.Panel( "cell" );
				panel.Style.Position = PositionMode.Absolute;
				CreatedPanels[i] = panel;
				CellData[i] = Data[i];
				OnCellCreated( i, panel );
			}

			Layout.Position( i, panel );
		}

		/// <summary>
		/// Create a new panel. You should add a child to the passed panel (which is the cell).
		/// </summary>
		public Action<Panel, object> OnCreateCell { get; set; }

		public virtual void OnCellCreated( int i, Panel cell )
		{
			var data = Data[i];

			if ( OnCreateCell != null )
			{
				OnCreateCell( cell, data );
			}
		}

		protected override void FinalLayoutChildren( Vector2 offset )
		{
			foreach ( var entry in CreatedPanels )
			{
				entry.Value.FinalLayout( offset );
			}

			var rect = Box.Rect;
			rect.Position -= ScrollOffset;
			rect.Height = MathF.Max( Layout.GetHeight( Data.Count ) * ScaleToScreen, rect.Height );

			ConstrainScrolling( rect.Size );
		}

		public override void SetProperty( string name, string value )
		{
			if ( name == "item-width" )
			{
				Layout.ItemWidth = Length.Parse( value ) ?? 100.0f;
			}

			if ( name == "item-height" )
			{
				Layout.ItemHeight = Length.Parse( value ) ?? 100.0f;
			}

			if ( name == "auto-columns" )
			{
				Layout.AutoColumns = value.ToBool();
			}

			base.SetProperty( name, value );
		}

	}

	public abstract class BaseVirtualScrollPanel<T> : Panel where T : class
	{
		private Dictionary<int, T> CellData = new Dictionary<int, T>();
		public Dictionary<int, Panel> CreatedPanels = new Dictionary<int, Panel>();

		List<int> RemoveQueue = new List<int>();

		public GridLayout Layout = new GridLayout();

		public bool NeedsRebuild { get; set; }

		internal int LastHash;

		public BaseVirtualScrollPanel()
		{
			Style.Overflow = OverflowMode.Scroll;
		}

		public override void Tick()
		{
			base.Tick();

			if ( ComputedStyle == null ) return;
			if ( !IsVisible ) return;

			var hash = HashCode.Combine( ItemCount() );
			if ( hash != LastHash )
			{
				LastHash = hash;
				NeedsRebuild = true;
			}

			if ( Layout.Update( Box, ScaleFromScreen, ScrollOffset.y * ScaleFromScreen, ComputedStyle.JustifyContent.Value ) || NeedsRebuild )
			{
				NeedsRebuild = false;
				Layout.GetVisibleRange( out var firstVisibleIndex, out var lastVisibleIndex );

				DeleteNotVisible( firstVisibleIndex, lastVisibleIndex );

				for ( int i = firstVisibleIndex; i < lastVisibleIndex; i++ )
				{
					RefreshCreated( i );
				}

			}
		}

		void DeleteNotVisible( int min, int max )
		{
			RemoveQueue.Clear();
			RemoveQueue.AddRange( CreatedPanels.Keys.Where( x => x < min || x > max || !HasData( x ) ) );

			for ( int i = 0; i < RemoveQueue.Count; i++ )
			{
				var idx = RemoveQueue[i];
				if ( CreatedPanels.Remove( idx, out var panel ) )
				{
					panel.Delete( true );
				}
			}
			RemoveQueue.Clear();
		}

		/// <summary>
		/// Return true if we have this data slot
		/// </summary>
		public abstract bool HasData( int i );
		public abstract int ItemCount();
		public abstract T GetItem( int i );

		public void RefreshCreated( int i )
		{
			if ( !HasData( i ) || i < 0 ) return;

			var data = GetItem( i );

			bool needsRebuild = true;
			if ( CellData.TryGetValue( i, out var lastData ) )
			{
				needsRebuild = lastData != data;
			}

			if ( !CreatedPanels.TryGetValue( i, out var panel ) || needsRebuild )
			{
				if ( needsRebuild )
				{
					panel?.Delete();
				}

				panel = Add.Panel( "cell" );
				panel.Style.Position = PositionMode.Absolute;
				CreatedPanels[i] = panel;
				CellData[i] = data;
				OnCellCreated( i, data, panel );
			}

			Layout.Position( i, panel );
		}

		public abstract void OnCellCreated( int index, T item, Panel cell );

		protected override void FinalLayoutChildren( Vector2 offset )
		{
			foreach ( var entry in CreatedPanels )
			{
				entry.Value.FinalLayout( offset );
			}

			var rect = Box.Rect;
			rect.Position -= ScrollOffset;
			rect.Height = MathF.Max( Layout.GetHeight( ItemCount() ) * ScaleToScreen, rect.Height );

			ConstrainScrolling( rect.Size );
		}

		public override void SetProperty( string name, string value )
		{
			if ( name == "item-width" )
			{
				Layout.ItemWidth = Length.Parse( value ) ?? 100.0f;
			}

			if ( name == "item-height" )
			{
				Layout.ItemHeight = Length.Parse( value ) ?? 100.0f;
			}

			if ( name == "auto-columns" )
			{
				Layout.AutoColumns = value.ToBool();
			}

			base.SetProperty( name, value );
		}

	}

}
