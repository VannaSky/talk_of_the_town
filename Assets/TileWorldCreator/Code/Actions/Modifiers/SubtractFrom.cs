using System;
using TWC.editor;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace TWC.Actions
{
   /// <summary>
   ///   A Tile World Creator modifier action that subtracts the current map from another named blueprint layer.  It is
   ///   the opposite of the built-in "Subtract" modifier; if Subtract performs the operation A - B, Subtract from
   ///   performs B - A.
   /// </summary>
   [ActionCategory( Category = ActionCategoryAttribute.CategoryTypes.Modifiers )]
   [ActionName( Name = "Subtract From" )]
   [Serializable]
   public sealed class SubtractFrom : TWCBlueprintAction, ITWCAction
   {
      private const float DefaultGuiHeight = 18.0f;


      #region > > > > >   Exposed Fields   < < < < <

      [SerializeField]
      private Guid _overlapLayer;

      #endregion


      private TWCGUILayout _guiLayout;


      private class GenericMenuData
      {
         public int              SelectedIndex;
         public TileWorldCreator Twc;
      }


      /// <summary>
      ///    Create a copy of this blueprint layer.
      /// </summary>
      /// <returns>
      ///    A deep copy of this blueprint layer.
      /// </returns>
      public ITWCAction Clone()
      {
         return new SubtractFrom()
         {
            _overlapLayer = this._overlapLayer
         };
      }


      /// <summary>
      ///    Perform an overlap (boolean AND) between this blueprint layer's current map and another named blueprint
      ///    layer.  The current map is updated in-place with the result.
      /// </summary>
      /// <param name="map">
      ///    The current state of this blueprint layer's map.
      /// </param>
      /// <param name="twc">
      ///    The TileWorldCreator instance controlling the blueprint layer.
      /// </param>
      /// <returns>
      ///    The array holding the result of merging the two maps.
      /// </returns>
      public bool [ , ] Execute( bool [ , ] map, TileWorldCreator twc )
      {
         bool [ , ] fromMap = twc.GetMapOutputFromBlueprintLayer( _overlapLayer );

         if ( null == fromMap )
         {
            Debug.LogWarning( "TileWorldCreator: modifier \"Subtract From\" - Blueprint layer is not assigned" );

            return map;
         }

         int _mapSizeX = fromMap.GetLength(0);
			int _mapSizeY = fromMap.GetLength(1);

         for ( int x = 0; x < _mapSizeX; x++ )
         {
            for ( int y = 0; y < _mapSizeY; y++ )
            {
               map [ x, y ] = !map[x,y] && fromMap [ x, y ];
            }
         }

         return map;
      }


#if UNITY_EDITOR
      public override void DrawGUI( Rect rect, int layerIndex, TileWorldCreatorAsset asset, TileWorldCreator twc )
      {
         using ( _guiLayout = new TWCGUILayout( rect ) )
         {
            string [] names     = EditorUtilities.GetAllGenerationLayerNames( asset );
            string    layerName = asset.GetBlueprintLayerData( _overlapLayer )?.layerName;

            _guiLayout.Add();

            if ( EditorGUI.DropdownButton( _guiLayout.rect, new GUIContent( layerName ), FocusType.Keyboard ) )
            {
               GenericMenu menu = new GenericMenu();

               for ( int n = 0; n < names.Length; n++ )
               {
                  GenericMenuData data = new GenericMenuData
                  {
                     SelectedIndex = n,
                     Twc           = twc
                  };

                  menu.AddItem( new GUIContent( names [ n ] ), false, AssignLayer, data );
               }

               menu.ShowAsContext();
            }
         }
      }


      private void AssignLayer( object data )
      {
         GenericMenuData d = data as GenericMenuData;

         // ReSharper disable once PossibleNullReferenceException
         //   -- will only be null if there's an error in Execute().

         _overlapLayer = d.Twc.twcAsset.mapBlueprintLayers [ d.SelectedIndex ].guid;
      }
#endif


      public float GetGUIHeight()
      {
         return _guiLayout == null ? DefaultGuiHeight : _guiLayout.height;
      }
   }
}
