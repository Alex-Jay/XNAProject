#define DEMO

using GDLibrary;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Media;
using System;

/*
 * Diffuse color not pickup up when drawing primitive objects
 * TranslationLerpController
 * Is frustum culling working on primitive objects?
 * 
 * 
 * No statustype on controllers
 * mouse object text interleaving with progress controller
 * Z-fighting on ground plane in 3rd person mode
 * Elevation angle on 3rd person view
 * PiP
 * menu transparency
*/

namespace GDApp
{
    public class Main : Game
    {
        #region Statics
        private readonly Color GoogleGreenColor = new Color(152, 234, 224, 225);
        #endregion

        #region Fields
#if DEBUG
        //used to visualize debug info (e.g. FPS) and also to draw collision skins
        private DebugDrawer debugDrawer;
#endif

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        //added property setters in short-hand form for speed
        public ObjectManager objectManager { get; private set; }
        public CameraManager cameraManager { get; private set; }
        public MouseManager mouseManager { get; private set; }
        public KeyboardManager keyboardManager { get; private set; }
        public ScreenManager screenManager { get; private set; }
        public MyAppMenuManager menuManager { get; private set; }
        public UIManager uiManager { get; private set; }
        public GamePadManager gamePadManager { get; private set; }
        public SoundManager soundManager { get; private set; }
        public MySimplePickingManager pickingManager { get; private set; }

        //receives, handles and routes events
        public EventDispatcher eventDispatcher { get; private set; }

        //stores loaded game resources
        private ContentDictionary<Model> modelDictionary;
        private ContentDictionary<Texture2D> textureDictionary;
        private ContentDictionary<SpriteFont> fontDictionary;

        //stores curves and rails used by cameras, viewport, effect parameters
        private Dictionary<string, Transform3DCurve> curveDictionary;
        private Dictionary<string, RailParameters> railDictionary;
        private Dictionary<string, Viewport> viewPortDictionary;
        private Dictionary<string, EffectParameters> effectDictionary;
        private ContentDictionary<Video> videoDictionary;
        private Dictionary<string, IVertexData> vertexDataDictionary;

        private ManagerParameters managerParameters;
        private MyPrimitiveFactory primitiveFactory;
        private PlayerCollidablePrimitiveObject playerCollidablePrimitiveObject;
        private PrimitiveDebugDrawer collisionSkinDebugDrawer;



        #endregion

        #region Properties
        #endregion

        #region Constructor
        public Main()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }
        #endregion

        #region Initialization
        protected override void Initialize()
        {
            //moved instanciation here to allow menu and ui managers to be moved to InitializeManagers()
            spriteBatch = new SpriteBatch(GraphicsDevice);

            bool isMouseVisible = true;
            Integer2 screenResolution = ScreenUtility.HD720;
            ScreenUtility.ScreenType screenType = ScreenUtility.ScreenType.SingleScreen;
            int numberOfGamePadPlayers = 1;

            //set the title
            Window.Title = "3DGD - My Amazing Individual Game 1.0";

            //EventDispatcher
            InitializeEventDispatcher();

            //Dictionaries, Media Assets and Non-media Assets
            LoadDictionaries();
            LoadAssets();
            LoadCurvesAndRails();
            LoadViewports(screenResolution);

            //factory to produce primitives
            LoadFactories();

            //Effects
            InitializeEffects();

            //Managers
            InitializeManagers(screenResolution, screenType, isMouseVisible, numberOfGamePadPlayers);

            //Add Menu and UI elements
            AddMenuElements();
            AddUIElements();

            //Load level contents
            int demoNumber = 8;

            if (demoNumber == 1)
                LoadDemoOne();  //unlit primitives
            else if (demoNumber == 2)
                LoadDemoTwo(); //lit primitives
            else if (demoNumber == 3)
                LoadDemoThree(); //level loader
            else if (demoNumber == 4)
                LoadDemoFour();  //mouse picking
            else if (demoNumber == 5)
                LoadDemoFive(); //zones and camera change
            else if (demoNumber == 6)
                LoadDemoSix(); //colliding with solid objects like architecture
            else if (demoNumber == 7)
                LoadDemoSeven(); //collecting ammo
            else 
                LoadDemoEight(); //turning on the controllers of objects that we collide with

            //Add Camera(s)
            InitializeCameraDemo(screenResolution);

            //Publish Start Event(s)
            StartGame();

#if DEBUG
            InitializeDebugTextInfo();
            InitializeDebugCollisionSkinInfo();
#endif

            base.Initialize();
        }

       
        private void InitializeManagers(Integer2 screenResolution, 
            ScreenUtility.ScreenType screenType, bool isMouseVisible, int numberOfGamePadPlayers) //1 - 4
        {
            //add sound manager
            this.soundManager = new SoundManager(this, this.eventDispatcher, StatusType.Update, "Content/Assets/Audio/", "Demo2DSound.xgs", "WaveBank1.xwb", "SoundBank1.xsb");
            Components.Add(this.soundManager);

            this.cameraManager = new CameraManager(this, 1, this.eventDispatcher);
            Components.Add(this.cameraManager);

            //create the object manager - notice that its not a drawablegamecomponent. See ScreeManager::Draw()
            this.objectManager = new ObjectManager(this, this.cameraManager, this.eventDispatcher, 10);
            
            //add keyboard manager
            this.keyboardManager = new KeyboardManager(this);
            Components.Add(this.keyboardManager);

            //create the manager which supports multiple camera viewports
            this.screenManager = new ScreenManager(this, graphics, screenResolution, screenType,
                this.objectManager, this.cameraManager, this.keyboardManager,
                AppData.KeyPauseShowMenu, this.eventDispatcher, StatusType.Off);
            this.screenManager.DrawOrder = 0;
            Components.Add(this.screenManager);
      
            //add mouse manager
            this.mouseManager = new MouseManager(this, isMouseVisible);
            Components.Add(this.mouseManager);

            //add gamepad manager
            if (numberOfGamePadPlayers > 0)
            {
                this.gamePadManager = new GamePadManager(this, numberOfGamePadPlayers);
                Components.Add(this.gamePadManager);
            }

            //menu manager
            this.menuManager = new MyAppMenuManager(this, this.mouseManager, this.keyboardManager, this.cameraManager, spriteBatch, this.eventDispatcher, StatusType.Off);
            //set the main menu to be the active menu scene
            this.menuManager.SetActiveList("mainmenu");
            this.menuManager.DrawOrder = 3;
            Components.Add(this.menuManager);

            //ui (e.g. reticule, inventory, progress)
            this.uiManager = new UIManager(this, this.spriteBatch, this.eventDispatcher, 10, StatusType.Off);
            this.uiManager.DrawOrder = 4;
            Components.Add(this.uiManager);
        
            //this object packages together all managers to give the mouse object the ability to listen for all forms of input from the user, as well as know where camera is etc.
            this.managerParameters = new ManagerParameters(this.objectManager,
                this.cameraManager, this.mouseManager, this.keyboardManager, this.gamePadManager, this.screenManager, this.soundManager);


            //used for simple picking (i.e. non-JigLibX)
            this.pickingManager = new MySimplePickingManager(this, this.eventDispatcher, StatusType.Update, this.managerParameters);
            Components.Add(this.pickingManager);

        }

        private void LoadDictionaries()
        {
            //models
            this.modelDictionary = new ContentDictionary<Model>("model dictionary", this.Content);

            //textures
            this.textureDictionary = new ContentDictionary<Texture2D>("texture dictionary", this.Content);

            //fonts
            this.fontDictionary = new ContentDictionary<SpriteFont>("font dictionary", this.Content);

            //curves - notice we use a basic Dictionary and not a ContentDictionary since curves and rails are NOT media content
            this.curveDictionary = new Dictionary<string, Transform3DCurve>();

            //rails
            this.railDictionary = new Dictionary<string, RailParameters>();

            //viewports - used to store different viewports to be applied to multi-screen layouts
            this.viewPortDictionary = new Dictionary<string, Viewport>();

            //stores default effect parameters
            this.effectDictionary = new Dictionary<string, EffectParameters>();

            //notice we go back to using a content dictionary type since we want to pass strings and have dictionary load content
            this.videoDictionary = new ContentDictionary<Video>("video dictionary", this.Content);

            //used to store IVertexData (i.e. when we want to draw primitive objects, as in I-CA)
            this.vertexDataDictionary = new Dictionary<string, IVertexData>();

        }
        private void LoadAssets()
        {
            #region Textures
            //environment
            this.textureDictionary.Load("Assets/Textures/Props/Crates/crate1"); //demo use of the shorter form of Load() that generates key from asset name
            this.textureDictionary.Load("Assets/Textures/Props/Crates/crate2");
            this.textureDictionary.Load("Assets/GDDebug/Textures/checkerboard");
            this.textureDictionary.Load("Assets/Textures/Foliage/Ground/grass1");
            this.textureDictionary.Load("Assets/Textures/Skybox/back");
            this.textureDictionary.Load("Assets/Textures/Skybox/left");
            this.textureDictionary.Load("Assets/Textures/Skybox/right");
            this.textureDictionary.Load("Assets/Textures/Skybox/sky");
            this.textureDictionary.Load("Assets/Textures/Skybox/front");
            this.textureDictionary.Load("Assets/Textures/Foliage/Trees/tree2");

            this.textureDictionary.Load("Assets/Textures/Semitransparent/transparentstripes");


            //menu - buttons
            this.textureDictionary.Load("Assets/Textures/UI/Menu/Buttons/genericbtn");

            //menu - backgrounds
            this.textureDictionary.Load("Assets/Textures/UI/Menu/Backgrounds/mainmenu");
            this.textureDictionary.Load("Assets/Textures/UI/Menu/Backgrounds/audiomenu");
            this.textureDictionary.Load("Assets/Textures/UI/Menu/Backgrounds/controlsmenu");
            this.textureDictionary.Load("Assets/Textures/UI/Menu/Backgrounds/exitmenuwithtrans");

            //ui (or hud) elements
            this.textureDictionary.Load("Assets/Textures/UI/HUD/reticuleDefault");
            this.textureDictionary.Load("Assets/Textures/UI/HUD/progress_gradient");


            //dual texture demo - see Main::InitializeCollidableGround()
            this.textureDictionary.Load("Assets/GDDebug/Textures/checkerboard_greywhite");

            //levels
            this.textureDictionary.Load("Assets/Textures/Level/level1");


#if DEBUG
            //demo
            this.textureDictionary.Load("Assets/GDDebug/Textures/ml");
            this.textureDictionary.Load("Assets/GDDebug/Textures/checkerboard");
#endif
            #endregion

            #region Fonts
#if DEBUG
            this.fontDictionary.Load("Assets/GDDebug/Fonts/debug");
#endif
            this.fontDictionary.Load("Assets/Fonts/menu");
            this.fontDictionary.Load("Assets/Fonts/mouse");
            #endregion

            #region Video
            this.videoDictionary.Load("Assets/Video/sample");
            #endregion
        }
        private void LoadCurvesAndRails()
        {
            int cameraHeight = 5;

            #region Curves
            //create the camera curve to be applied to the track controller
            Transform3DCurve curveA = new Transform3DCurve(CurveLoopType.Oscillate); //experiment with other CurveLoopTypes
            curveA.Add(new Vector3(40, cameraHeight, 80), -Vector3.UnitX, Vector3.UnitY, 0); //start position
            curveA.Add(new Vector3(0, 10, 60), -Vector3.UnitZ, Vector3.UnitY, 4);
            curveA.Add(new Vector3(0, 40, 0), -Vector3.UnitY, -Vector3.UnitZ, 8); //curve mid-point
            curveA.Add(new Vector3(0, 10, 60), -Vector3.UnitZ, Vector3.UnitY, 12);
            curveA.Add(new Vector3(40, cameraHeight, 80), -Vector3.UnitX, Vector3.UnitY, 16); //end position - same as start for zero-discontinuity on cycle
            //add to the dictionary
            this.curveDictionary.Add("unique curve name 1", curveA);
            #endregion

            #region Rails
            //create the track to be applied to the non-collidable track camera 1
            this.railDictionary.Add("rail1 - parallel to x-axis", new RailParameters("rail1 - parallel to x-axis", new Vector3(-80, 10, 40), new Vector3(80, 10, 40)));
            #endregion

        }
        private void LoadViewports(Integer2 screenResolution)
        {

            //the full screen viewport with optional padding
            int leftPadding = 0, topPadding = 0, rightPadding = 0, bottomPadding = 0;
            Viewport paddedFullViewPort = ScreenUtility.Pad(new Viewport(0, 0, screenResolution.X, (int)(screenResolution.Y)), leftPadding, topPadding, rightPadding, bottomPadding);
            this.viewPortDictionary.Add("full viewport", paddedFullViewPort);

            //work out the dimensions of the small camera views along the left hand side of the screen
            int smallViewPortHeight = 144; //6 small cameras along the left hand side of the main camera view i.e. total height / 5 = 720 / 5 = 144
            int smallViewPortWidth = 5 * smallViewPortHeight / 3; //we should try to maintain same ProjectionParameters aspect ratio for small cameras as the large     
            //the five side viewports in multi-screen mode
            this.viewPortDictionary.Add("column0 row0", new Viewport(0, 0, smallViewPortWidth, smallViewPortHeight));
            this.viewPortDictionary.Add("column0 row1", new Viewport(0, 1 * smallViewPortHeight, smallViewPortWidth, smallViewPortHeight));
            this.viewPortDictionary.Add("column0 row2", new Viewport(0, 2 * smallViewPortHeight, smallViewPortWidth, smallViewPortHeight));
            this.viewPortDictionary.Add("column0 row3", new Viewport(0, 3 * smallViewPortHeight, smallViewPortWidth, smallViewPortHeight));
            this.viewPortDictionary.Add("column0 row4", new Viewport(0, 4 * smallViewPortHeight, smallViewPortWidth, smallViewPortHeight));
            //the larger view to the right in column 1
            this.viewPortDictionary.Add("column1 row0", new Viewport(smallViewPortWidth, 0, screenResolution.X - smallViewPortWidth, screenResolution.Y));

            //picture-in-picture viewport
            Integer2 viewPortDimensions = new Integer2(240, 150); //set to 16:10 ratio as with screen dimensions
            int verticalOffset = 20;
            int rightHorizontalOffset = 20;
            this.viewPortDictionary.Add("PIP viewport", new Viewport((screenResolution.X - viewPortDimensions.X - rightHorizontalOffset), 
                verticalOffset,  viewPortDimensions.X, viewPortDimensions.Y));
        }
        private void LoadFactories()
        {
            this.primitiveFactory = new MyPrimitiveFactory();
        }

#if DEBUG
        private void InitializeDebugTextInfo()
        {
            //add debug info in top left hand corner of the screen
            this.debugDrawer = new DebugDrawer(this, this.managerParameters, spriteBatch,
                this.fontDictionary["debug"], Color.Black, new Vector2(5, 5), this.eventDispatcher, StatusType.Off);
            this.debugDrawer.DrawOrder = 1;
            Components.Add(this.debugDrawer);

        }

        //draws the frustum culling spheres, the collision primitive surfaces, and the zone object collision primitive surfaces based on booleans passed
        private void InitializeDebugCollisionSkinInfo()
        {
            int primitiveCount = 0;
            PrimitiveType primitiveType;

            //used to draw spherical collision surfaces
            IVertexData sphereVertexData = new BufferedVertexData<VertexPositionColor>(
                graphics.GraphicsDevice, PrimitiveUtility.GetWireframeSphere(5, out primitiveType, out primitiveCount), primitiveType, primitiveCount);

            this.collisionSkinDebugDrawer = new PrimitiveDebugDrawer(this, this.eventDispatcher, StatusType.Update | StatusType.Drawn,
                this.managerParameters, true, true, true, sphereVertexData);
            collisionSkinDebugDrawer.DrawOrder = 2;
            Components.Add(collisionSkinDebugDrawer);

        }
#endif
        #endregion

        #region Load Demo Eight
        private void LoadDemoEight()
        {
            //non-collidable ground
            int worldScale = 250;
            InitializeNonCollidableGround(worldScale);

            //collidable and drivable player
            InitializeCollidablePlayer();

            //collidable objects that we can turn on when we hit them
            InitializeCollidableActivatableObjects();
        }

        private void InitializeCollidableActivatableObjects()
        {
            //get the effect relevant to this primitive type (i.e. colored, textured, wireframe, lit, unlit)
            BasicEffectParameters effectParameters = this.effectDictionary[AppData.LitTexturedPrimitivesEffectID] as BasicEffectParameters;

            //get the archetypal primitive object from the factory
            PrimitiveObject archetypeObject = this.primitiveFactory.GetArchetypePrimitiveObject(graphics.GraphicsDevice, ShapeType.NormalCube, effectParameters);

            //set the texture that all clones will have
            archetypeObject.EffectParameters.Texture = this.textureDictionary["checkerboard"];

            Transform3D transform;
            CollidablePrimitiveObject collidablePrimitiveObject;
            IController controller;

            for (int i = 0; i < 4; i++)
            {
                //remember the primitive is at Transform3D.Zero so we need to say where we want OUR player to start
                transform = new Transform3D(new Vector3(5 * i + 10, 2, 0), Vector3.Zero, new Vector3(2, 2, 2), Vector3.UnitX, Vector3.UnitY);

                //make the collidable primitive
                collidablePrimitiveObject = new CollidablePrimitiveObject(archetypeObject.Clone() as PrimitiveObject,
                    new BoxCollisionPrimitive(transform), this.objectManager);

                //do we want an actor type for CDCR?
                collidablePrimitiveObject.ActorType = ActorType.CollidableActivatable;

                //set the position otherwise the boxes will all have archetypeObject.Transform positional properties
                collidablePrimitiveObject.Transform = transform;

                //#region Translation Lerp
                ////if we want to make the boxes move (or do something else) then just attach a controller
                //controller = new TranslationSineLerpController("transControl1", ControllerType.LerpTranslation,
                //    new Vector3(0, 0, 1), new TrigonometricParameters(10, 0.1f, 180 * (i - 5)));
                //collidablePrimitiveObject.AttachController(controller);
                //#endregion

                //#region Sine Lerp
                ////lets add a color lerp controller but set it not to play until the player touches it
                //controller = new ColorSineLerpController("colorControl1", ControllerType.SineColorLerp,
                //    Color.Red, Color.Green, new TrigonometricParameters(1, 0.2f, 180 * (i - 5)));
                ////lets turn off the color lerp until we touch the box - see PlayerCollidablePrimitiveObject::HandleCollisionResponse()
                //controller.SetControllerPlayStatus(PlayStatusType.Off);
                //collidablePrimitiveObject.AttachController(controller);
                //#endregion

                #region Pickup Controller
                controller = new PickupController("pickupControl1", ControllerType.PickupDisappear, 15, 0.02f * Vector3.UnitY, 0.99f * Vector3.One, -0.02f, 0.1f);
                controller.SetControllerPlayStatus(PlayStatusType.Off);
                collidablePrimitiveObject.AttachController(controller);
                #endregion

                this.objectManager.Add(collidablePrimitiveObject);
            }
        }

        #endregion

        #region Load Demo Seven 
        private void LoadDemoSeven()
        {
            //non-collidable ground
            int worldScale = 250;
            InitializeNonCollidableGround(worldScale);

            //collidable and drivable player
            InitializeCollidablePlayer();

            //collidable ammo that we can pickup
            InitializeCollidableAmmo();
        }

        private void InitializeCollidableAmmo()
        {
            //get the effect relevant to this primitive type (i.e. colored, textured, wireframe, lit, unlit)
            BasicEffectParameters effectParameters = this.effectDictionary[AppData.LitTexturedPrimitivesEffectID] as BasicEffectParameters;

            //get the archetypal primitive object from the factory
            PrimitiveObject archetypeObject = this.primitiveFactory.GetArchetypePrimitiveObject(graphics.GraphicsDevice, ShapeType.NormalCube, effectParameters);

            //set the texture that all clones will have
            archetypeObject.EffectParameters.Texture = this.textureDictionary["checkerboard"];
            //make diffuse green
            archetypeObject.EffectParameters.DiffuseColor = Color.Green;

            Transform3D transform;
            CollidablePrimitiveObject collidablePrimitiveObject;

            for (int i = -4; i < 6; i++)
            {
                //remember the primitive is at Transform3D.Zero so we need to say where we want OUR player to start
                transform = new Transform3D(new Vector3(10, 2, 5 * i), Vector3.Zero, new Vector3(1, 2, 3), Vector3.UnitX, Vector3.UnitY);

                //make the collidable primitive
                collidablePrimitiveObject = new CollidablePrimitiveObject(archetypeObject.Clone() as PrimitiveObject,
                    new BoxCollisionPrimitive(transform), this.objectManager);

                //do we want an actor type for CDCR?
                collidablePrimitiveObject.ActorType = ActorType.CollidableAmmo;

                //set the position otherwise the boxes will all have archetypeObject.Transform positional properties
                collidablePrimitiveObject.Transform = transform;

                this.objectManager.Add(collidablePrimitiveObject);
            }
        }
        #endregion

        #region Load Demo Six 
        private void LoadDemoSix()
        {
            //non-collidable ground
            int worldScale = 250;
            InitializeNonCollidableGround(worldScale);

            //collidable and drivable player
            InitializeCollidablePlayer();

            //collidable ammo that we can pickup
            InitializeCollidableDecorators();

        }
        #endregion

        #region Load Demo Five 
        private void LoadDemoFive()
        {
            //non-collidable ground
            int worldScale = 250;
            InitializeNonCollidableGround(worldScale);

            //collidable and drivable player
            InitializeCollidablePlayer();

            //zones that we can use to generate events based on user position e.g. change the camera
            InitializeCollidableZones();
        }
        private void InitializeCollidableDecorators()
        {
            //get the effect relevant to this primitive type (i.e. colored, textured, wireframe, lit, unlit)
            BasicEffectParameters effectParameters = this.effectDictionary[AppData.LitTexturedPrimitivesEffectID] as BasicEffectParameters;

            //get the archetypal primitive object from the factory
            PrimitiveObject archetypeObject = this.primitiveFactory.GetArchetypePrimitiveObject(graphics.GraphicsDevice, ShapeType.NormalCube, effectParameters);

            //set the texture that all clones will have
            archetypeObject.EffectParameters.Texture = this.textureDictionary["checkerboard"];

            Transform3D transform;
            CollidablePrimitiveObject collidablePrimitiveObject;

            for (int i = 0; i < 10; i++)
            {
                //remember the primitive is at Transform3D.Zero so we need to say where we want OUR player to start
                transform = new Transform3D(new Vector3(5 * i + 10, 2, 0), Vector3.Zero, new Vector3(2, 2, 2), Vector3.UnitX, Vector3.UnitY);

                //make the collidable primitive
                collidablePrimitiveObject = new CollidablePrimitiveObject(archetypeObject.Clone() as PrimitiveObject, 
                    new BoxCollisionPrimitive(transform), this.objectManager);

                //do we want an actor type for CDCR?
                collidablePrimitiveObject.ActorType = ActorType.CollidableDecorator;

                //set the position otherwise the boxes will all have archetypeObject.Transform positional properties
                collidablePrimitiveObject.Transform = transform;

                this.objectManager.Add(collidablePrimitiveObject);
            }
        }

        private void InitializeCollidableZones()
        {
            Transform3D transform = null;
            SimpleZoneObject simpleZoneObject = null;
            ICollisionPrimitive collisionPrimitive = null;

            //place the zone and scale it based on how big you want the zone to be
            transform = new Transform3D(new Vector3(-10, 2, 0), new Vector3(1, 1, 1));

            //we can have a sphere or a box - its entirely up to the developer
            collisionPrimitive = new SphereCollisionPrimitive(transform, 2);
            //collisionPrimitive = new BoxCollisionPrimitive(transform);

            simpleZoneObject = new SimpleZoneObject(AppData.SwitchToThirdPersonZoneID, ActorType.Zone, transform,
                StatusType.Drawn | StatusType.Update, collisionPrimitive);//, mParams);

            this.objectManager.Add(simpleZoneObject);
        }

        #endregion

        #region Load Demo Four 
        private void LoadDemoFour()
        {
            //add helpers to help us orient ourself
            InitializeHelperPrimitives();

            //non-collidable ground
            int worldScale = 250;
            InitializeNonCollidableGround(worldScale);

            //collidable and drivable player
            InitializeCollidablePlayer();
        }

        private void InitializeNonCollidableGround(int worldScale)
        {
            //get the effect relevant to this primitive type (i.e. colored, textured, wireframe, lit, unlit)
            BasicEffectParameters effectParameters = this.effectDictionary[AppData.LitTexturedPrimitivesEffectID] as BasicEffectParameters;

            //get the primitive object from the factory (remember the factory returns a clone)
            PrimitiveObject ground = this.primitiveFactory.GetArchetypePrimitiveObject(graphics.GraphicsDevice, ShapeType.NormalCube, effectParameters);

            //set the texture
            ground.EffectParameters.Texture = this.textureDictionary["grass1"];

            //set the transform
            //since the object is 1 unit in height, we move it down to Y-axis == -0.5f so that the top of the surface is at Y == 0
            ground.Transform = new Transform3D(new Vector3(0, -0.5f, 0), new Vector3(worldScale, 1, worldScale));

            //set an ID if we want to access this later
            ground.ID = "non-collidable ground";

            //add 
            this.objectManager.Add(ground);

        }

        private void InitializeCollidablePlayer()
        {
            //get the effect relevant to this primitive type (i.e. colored, textured, wireframe, lit, unlit)
            BasicEffectParameters effectParameters = this.effectDictionary[AppData.LitTexturedPrimitivesEffectID] as BasicEffectParameters;
            
            //get the archetypal primitive object from the factory
            PrimitiveObject primitiveObject = this.primitiveFactory.GetArchetypePrimitiveObject(graphics.GraphicsDevice, ShapeType.NormalCube, effectParameters);
       
            //remember the primitive is at Transform3D.Zero so we need to say where we want OUR player to start
            Transform3D transform = new Transform3D(new Vector3(5, 2, 0), Vector3.Zero, new Vector3(1, 4, 1), Vector3.UnitX, Vector3.UnitY);

            //instanciate a box primitive at player position
            BoxCollisionPrimitive collisionPrimitive = new BoxCollisionPrimitive(transform);
            
            //make the player object and store as field for use by the 3rd person camera - see camera initialization
            this.playerCollidablePrimitiveObject = new PlayerCollidablePrimitiveObject(primitiveObject, collisionPrimitive, 
                this.managerParameters, AppData.PlayerOneMoveKeys, AppData.PlayerMoveSpeed, AppData.PlayerRotationSpeed);
            this.playerCollidablePrimitiveObject.ActorType = ActorType.Player;
            this.playerCollidablePrimitiveObject.Transform = transform;

            //do we want a texture?
            playerCollidablePrimitiveObject.EffectParameters.Texture = this.textureDictionary["ml"];

            //set an ID if we want to access this later
            playerCollidablePrimitiveObject.ID = "collidable player";

            //add to the object manager
            this.objectManager.Add(playerCollidablePrimitiveObject);
        }
        #endregion

        #region Load Demo Three
        //load the contents for the level loader demo
        private void LoadDemoThree()
        {
            //add helpers to help us orient ourself
            InitializeHelperPrimitives();

            InitializeLevelLoaderPrimitives();
        }

        private void InitializeLevelLoaderPrimitives()
        {
            LevelLoader levelLoader = new LevelLoader(this.textureDictionary, "level1");

            //get the effect relevant to this primitive type (i.e. colored, textured, wireframe, lit, unlit)
            BasicEffectParameters effectParameters = this.effectDictionary[AppData.LitTexturedPrimitivesEffectID] as BasicEffectParameters;
            
            //get the archetype from the factory
            PrimitiveObject crateA = this.primitiveFactory.GetArchetypePrimitiveObject(graphics.GraphicsDevice, ShapeType.NormalCube, effectParameters);

            //set texture once so all clones have the same
            crateA.EffectParameters.Texture = this.textureDictionary["crate1"];

            //define what each pixel color should generate
            levelLoader.AddColorActor3DPair(Color.Red, crateA);

            //process the pixels in the level texture and return a list of actors
            List<Actor3D> listOfActors = levelLoader.Process(new Vector2(1, 1), 0, Vector3.Zero);

            //add the actors to the object manager
            this.objectManager.Add(listOfActors);
        }
        #endregion

        #region Load Demo Two
        //load the contents for lit primitive demo
        private void LoadDemoTwo()
        {
            //add helpers to help us orient ourself
            InitializeHelperPrimitives();

            ////add lit primitives
            InitializeLitPrimitives();
        }
        private void InitializeLitPrimitives()
        {
            PrimitiveObject clonedObject = null;

            //get the effect relevant to this primitive type (i.e. colored, textured, wireframe, lit, unlit)
            BasicEffectParameters effectParameters = this.effectDictionary[AppData.LitTexturedPrimitivesEffectID] as BasicEffectParameters;

            //get the archetype from the factory
            PrimitiveObject archetypeCrate = this.primitiveFactory.GetArchetypePrimitiveObject(graphics.GraphicsDevice, ShapeType.NormalCube, effectParameters);

            //set texture once so all clones have the same
            archetypeCrate.EffectParameters.Texture = this.textureDictionary["crate1"];

            //make some copies
            for (int i = 1; i <= 50; i++)
            {
                //copy
                clonedObject = archetypeCrate.Clone() as PrimitiveObject;

                //re-position, scale
                clonedObject.Transform.Translation = new Vector3(-4 * i, 0, 0);

                if (i % 2 == 0)
                {
                    clonedObject.Transform.Scale = new Vector3(1, 2, 1);
                }

                //add to manager
                this.objectManager.Add(clonedObject);
            }
        }
        #endregion

        #region Load Demo One
        //load the contents for the unlit primitives demo
        private void LoadDemoOne()
        {
            //add helpers to help us orient ourself
            InitializeHelperPrimitives();

            ////add unlit quads
            InitializeQuadPrimitives();

            //add unlit cubes
            InitializeCubePrimitives();

        }
        private void InitializeQuadPrimitives()
        {
            PrimitiveObject clonedObject = null;

            //get the effect relevant to this primitive type (i.e. colored, textured, wireframe, lit, unlit)
            BasicEffectParameters effectParameters = this.effectDictionary[AppData.UnLitColoredPrimitivesEffectID] as BasicEffectParameters;
            effectParameters.DiffuseColor = Color.Red;

            //get the archetype from the factory
            PrimitiveObject archetypeQuad = this.primitiveFactory.GetArchetypePrimitiveObject(graphics.GraphicsDevice, ShapeType.ColoredQuad, effectParameters);

            //make some copies
            for (int i = 1; i <= 4; i++)
            {
                //copy
                clonedObject = archetypeQuad.Clone() as PrimitiveObject;
                
                //change id, re-position, scale and change alpha for fun
                clonedObject.ID = "Spinning thing " + i;
                clonedObject.Transform.Translation = new Vector3(0, 5 * i, 0);
                clonedObject.Transform.Scale = new Vector3(4 * i, 2, 1);
                clonedObject.EffectParameters.Alpha = i * 0.25f;
                
                //we could also attach controllers here instead to give each a different rotation
                clonedObject.AttachController(new RotationController("rot controller", ControllerType.Rotation, new Vector3(0.1f * i, 0, 0)));

                //add to manager
                this.objectManager.Add(clonedObject);
            }

        }
        private void InitializeCubePrimitives()
        {
            PrimitiveObject clonedObject = null;

            //get the effect relevant to this primitive type (i.e. colored, textured, wireframe, lit, unlit)
            BasicEffectParameters effectParameters = this.effectDictionary[AppData.UnLitTexturedPrimitivesEffectID] as BasicEffectParameters;
            
            //get the archetype from the factory
            PrimitiveObject archetypeObject = this.primitiveFactory.GetArchetypePrimitiveObject(graphics.GraphicsDevice, ShapeType.TexturedCube, effectParameters);
            
            //set texture once so all clones have the same
            archetypeObject.EffectParameters.Texture = this.textureDictionary["crate1"];

            //make some copies
            for(int i = 1; i <= 10; i++)
            {
                clonedObject = archetypeObject.Clone() as PrimitiveObject;

                //re-position, scale
                clonedObject.Transform.Translation = new Vector3(10 * i, 0, 0);

            //    if (i % 2 == 0)
            //    {
            //        clonedObject.Transform.Scale = new Vector3(1, 1, 1);
            //        clonedObject.EffectParameters.Alpha = 0.5f;
            //    }
            //    else
            //    {
            //        //we could also attach controllers here instead to give each a different translation
            ////        clonedObject.AttachController(new TranslationSineLerpController("trans controller", ControllerType.LerpTranslation, Vector3.UnitY, new TrigonometricParameters(1, 0.1f, 0)));

            //    }


                //add to manager
                this.objectManager.Add(clonedObject);
            }
        }
        #endregion

        #region Layout Helpers
        private void InitializeHelperPrimitives()
        {
            BasicEffectParameters effectParameters = this.effectDictionary[AppData.UnLitColoredPrimitivesEffectID] as BasicEffectParameters;
            //since its wireframe we dont set color, texture, alpha etc.

            //get the archetype from the factory
            PrimitiveObject archetypeObject = this.primitiveFactory.GetArchetypePrimitiveObject(graphics.GraphicsDevice, ShapeType.WireframeOrigin, effectParameters);

            //clone to set the unique properties of the origin helper
            PrimitiveObject originObject = archetypeObject.Clone() as PrimitiveObject;

            //make it a little more visible!
            originObject.Transform.Translation = new Vector3(0, 10, 0);
            originObject.Transform.Scale *= 4;

            //set an ID if we want to access this later
            originObject.ID = "origin helper";

            //add to the object manager
            this.objectManager.Add(originObject);

        }
        #endregion

        #region Initialize Cameras
        private void InitializeCamera(Integer2 screenResolution, string id, Viewport viewPort, Transform3D transform, IController controller, float drawDepth)
        {
            Camera3D camera = new Camera3D(id, ActorType.Camera, transform, ProjectionParameters.StandardShallowSixteenNine, viewPort, drawDepth, StatusType.Update);

            if (controller != null)
                camera.AttachController(controller);

            this.cameraManager.Add(camera);
        }

        //adds three camera from 3 different perspectives that we can cycle through
        private void InitializeCameraDemo(Integer2 screenResolution)
        {
            #region Flight Camera
            Transform3D transform = new Transform3D(new Vector3(0, 5, 20), -Vector3.UnitZ, Vector3.UnitY);

            IController controller = new FlightCameraController("fcc", ControllerType.FirstPerson, AppData.CameraMoveKeys,
                AppData.CameraMoveSpeed, AppData.CameraStrafeSpeed, AppData.CameraRotationSpeed, this.managerParameters);

            InitializeCamera(screenResolution, AppData.FlightCameraID, this.viewPortDictionary["full viewport"], transform, controller, 0);
            #endregion

            #region Third Person Camera
            if (this.playerCollidablePrimitiveObject != null) //if demo 4 then we have player to track
            {
                //position is irrelevant since its based on tracking a player object
                transform = Transform3D.Zero;

                controller = new ThirdPersonController("tpc", ControllerType.ThirdPerson, this.playerCollidablePrimitiveObject,
                    AppData.CameraThirdPersonDistance, AppData.CameraThirdPersonScrollSpeedDistanceMultiplier,
                    AppData.CameraThirdPersonElevationAngleInDegrees, AppData.CameraThirdPersonScrollSpeedElevationMultiplier,
                    LerpSpeed.Medium, LerpSpeed.Fast, this.mouseManager);

                InitializeCamera(screenResolution, AppData.ThirdPersonCameraID, this.viewPortDictionary["full viewport"], transform, controller, 0);
            }
            #endregion

        }

        #endregion

        #region Events
        private void InitializeEventDispatcher()
        {
            //initialize with an arbitrary size based on the expected number of events per update cycle, increase/reduce where appropriate
            this.eventDispatcher = new EventDispatcher(this, 20);

            //dont forget to add to the Component list otherwise EventDispatcher::Update won't get called and no event processing will occur!
            Components.Add(this.eventDispatcher);
        }

        private void StartGame()
        {
            //will be received by the menu manager and screen manager and set the menu to be shown and game to be paused
            EventDispatcher.Publish(new EventData(EventActionType.OnPause, EventCategoryType.MainMenu));

            //publish an event to set the camera
            object[] additionalEventParamsB = { "flight camera 1"};
            EventDispatcher.Publish(new EventData(EventActionType.OnCameraSetActive, EventCategoryType.Camera, additionalEventParamsB));
            //we could also just use the line below, but why not use our event dispatcher?
            //this.cameraManager.SetActiveCamera(x => x.ID.Equals("collidable first person camera 1"));
        }
        #endregion

        #region Menu & UI
        private void AddMenuElements()
        {
            Transform2D transform = null;
            Texture2D texture = null;
            Vector2 position = Vector2.Zero;
            UIButtonObject uiButtonObject = null, clone = null;
            string sceneID = "", buttonID = "", buttonText = "";
            int verticalBtnSeparation = 50;

            #region Main Menu
            sceneID = "main menu";

            //retrieve the background texture
            texture = this.textureDictionary["mainmenu"];
            //scale the texture to fit the entire screen
            Vector2 scale = new Vector2((float)graphics.PreferredBackBufferWidth / texture.Width,
                (float)graphics.PreferredBackBufferHeight / texture.Height);
            transform = new Transform2D(scale);

            this.menuManager.Add(sceneID, new UITextureObject("mainmenuTexture", ActorType.UIStaticTexture,
                StatusType.Drawn, //notice we dont need to update a static texture
                transform, Color.White, SpriteEffects.None,
                1, //depth is 1 so its always sorted to the back of other menu elements
                texture));

            //add start button
            buttonID = "startbtn";
            buttonText = "Start";
            position = new Vector2(graphics.PreferredBackBufferWidth / 2.0f, 200);
            texture = this.textureDictionary["genericbtn"];
            transform = new Transform2D(position,
                0, new Vector2(1.8f, 0.6f),
                new Vector2(texture.Width / 2.0f, texture.Height / 2.0f), new Integer2(texture.Width, texture.Height));

            uiButtonObject = new UIButtonObject(buttonID, ActorType.UIButton, StatusType.Update | StatusType.Drawn,
                transform, Color.LightPink, SpriteEffects.None, 0.1f, texture, buttonText,
                this.fontDictionary["menu"],
                Color.DarkGray, new Vector2(0, 2));

            uiButtonObject.AttachController(new UIScaleSineLerpController("sineScaleLerpController2", ControllerType.SineScaleLerp,
              new TrigonometricParameters(0.1f, 0.2f, 1)));
            this.menuManager.Add(sceneID, uiButtonObject);


            //add audio button - clone the audio button then just reset texture, ids etc in all the clones
            clone = (UIButtonObject)uiButtonObject.Clone();
            clone.ID = "audiobtn";
            clone.Text = "Audio";
            //move down on Y-axis for next button
            clone.Transform.Translation += new Vector2(0, verticalBtnSeparation);
            //change the texture blend color
            clone.Color = Color.LightGreen;
            this.menuManager.Add(sceneID, clone);

            //add controls button - clone the audio button then just reset texture, ids etc in all the clones
            clone = (UIButtonObject)uiButtonObject.Clone();
            clone.ID = "controlsbtn";
            clone.Text = "Controls";
            //move down on Y-axis for next button
            clone.Transform.Translation += new Vector2(0, 2 * verticalBtnSeparation);
            //change the texture blend color
            clone.Color = Color.LightBlue;
            this.menuManager.Add(sceneID, clone);

            //add exit button - clone the audio button then just reset texture, ids etc in all the clones
            clone = (UIButtonObject)uiButtonObject.Clone();
            clone.ID = "exitbtn";
            clone.Text = "Exit";
            //move down on Y-axis for next button
            clone.Transform.Translation += new Vector2(0, 3 * verticalBtnSeparation);
            //change the texture blend color
            clone.Color = Color.LightYellow;
            //store the original color since if we modify with a controller and need to reset
            clone.OriginalColor = clone.Color;
            //attach another controller on the exit button just to illustrate multi-controller approach
            clone.AttachController(new UIColorSineLerpController("colorSineLerpController", ControllerType.SineColorLerp,
                    new TrigonometricParameters(1, 0.4f, 0), Color.LightSeaGreen, Color.LightGreen));
            this.menuManager.Add(sceneID, clone);
            #endregion

            #region Audio Menu
            sceneID = "audio menu";

            //retrieve the audio menu background texture
            texture = this.textureDictionary["audiomenu"];
            //scale the texture to fit the entire screen
            scale = new Vector2((float)graphics.PreferredBackBufferWidth / texture.Width,
                (float)graphics.PreferredBackBufferHeight / texture.Height);
            transform = new Transform2D(scale);
            this.menuManager.Add(sceneID, new UITextureObject("audiomenuTexture", ActorType.UIStaticTexture,
                StatusType.Drawn, //notice we dont need to update a static texture
                transform, Color.White, SpriteEffects.None,
                1, //depth is 1 so its always sorted to the back of other menu elements
                texture));


            //add volume up button - clone the audio button then just reset texture, ids etc in all the clones
            clone = (UIButtonObject)uiButtonObject.Clone();
            clone.ID = "volumeUpbtn";
            clone.Text = "Volume Up";
            //change the texture blend color
            clone.Color = Color.LightPink;
            this.menuManager.Add(sceneID, clone);

            //add volume down button - clone the audio button then just reset texture, ids etc in all the clones
            clone = (UIButtonObject)uiButtonObject.Clone();
            //move down on Y-axis for next button
            clone.Transform.Translation += new Vector2(0, verticalBtnSeparation);
            clone.ID = "volumeDownbtn";
            clone.Text = "Volume Down";
            //change the texture blend color
            clone.Color = Color.LightGreen;
            this.menuManager.Add(sceneID, clone);

            //add volume mute button - clone the audio button then just reset texture, ids etc in all the clones
            clone = (UIButtonObject)uiButtonObject.Clone();
            //move down on Y-axis for next button
            clone.Transform.Translation += new Vector2(0, 2 * verticalBtnSeparation);
            clone.ID = "volumeMutebtn";
            clone.Text = "Volume Mute";
            //change the texture blend color
            clone.Color = Color.LightBlue;
            this.menuManager.Add(sceneID, clone);

            //add volume mute button - clone the audio button then just reset texture, ids etc in all the clones
            clone = (UIButtonObject)uiButtonObject.Clone();
            //move down on Y-axis for next button
            clone.Transform.Translation += new Vector2(0, 3 * verticalBtnSeparation);
            clone.ID = "volumeUnMutebtn";
            clone.Text = "Volume Un-mute";
            //change the texture blend color
            clone.Color = Color.LightSalmon;
            this.menuManager.Add(sceneID, clone);

            //add back button - clone the audio button then just reset texture, ids etc in all the clones
            clone = (UIButtonObject)uiButtonObject.Clone();
            //move down on Y-axis for next button
            clone.Transform.Translation += new Vector2(0, 4 * verticalBtnSeparation);
            clone.ID = "backbtn";
            clone.Text = "Back";
            //change the texture blend color
            clone.Color = Color.LightYellow;
            this.menuManager.Add(sceneID, clone);
            #endregion

            #region Controls Menu
            sceneID = "controls menu";

            //retrieve the controls menu background texture
            texture = this.textureDictionary["controlsmenu"];
            //scale the texture to fit the entire screen
            scale = new Vector2((float)graphics.PreferredBackBufferWidth / texture.Width,
                (float)graphics.PreferredBackBufferHeight / texture.Height);
            transform = new Transform2D(scale);
            this.menuManager.Add(sceneID, new UITextureObject("controlsmenuTexture", ActorType.UIStaticTexture,
                StatusType.Drawn, //notice we dont need to update a static texture
                transform, Color.White, SpriteEffects.None,
                1, //depth is 1 so its always sorted to the back of other menu elements
                texture));

            //add back button - clone the audio button then just reset texture, ids etc in all the clones
            clone = (UIButtonObject)uiButtonObject.Clone();
            //move down on Y-axis for next button
            clone.Transform.Translation += new Vector2(0, 9 * verticalBtnSeparation);
            clone.ID = "backbtn";
            clone.Text = "Back";
            //change the texture blend color
            clone.Color = Color.LightYellow;
            this.menuManager.Add(sceneID, clone);
            #endregion
        }
        private void AddUIElements()
        {
            InitializeUIMousePointer();
            InitializeUIProgress();
        }
        private void InitializeUIMousePointer()
        {
            Texture2D texture = this.textureDictionary["reticuleDefault"];
            //show complete texture
            Microsoft.Xna.Framework.Rectangle sourceRectangle = new Microsoft.Xna.Framework.Rectangle(0, 0, texture.Width, texture.Height);

            //listens for object picking events from the object picking manager
            UIPickingMouseObject myUIMouseObject = new UIPickingMouseObject("picking mouseObject",
                ActorType.UITexture,
                new Transform2D(Vector2.One),
                this.fontDictionary["mouse"],
                "",
                new Vector2(0, 40),
                texture,
                this.mouseManager,
                this.eventDispatcher);
            this.uiManager.Add(myUIMouseObject);
        }
        private void InitializeUIProgress()
        {
            float separation = 20; //spacing between progress bars

            Transform2D transform = null;
            Texture2D texture = null;
            UITextureObject textureObject = null;
            Vector2 position = Vector2.Zero;
            Vector2 scale = Vector2.Zero;
            float verticalOffset = 20;
            int startValue;

            texture = this.textureDictionary["progress_gradient"];
            scale = new Vector2(1, 0.75f);

            #region Player 1 Progress Bar
            position = new Vector2(graphics.PreferredBackBufferWidth / 2.0f - texture.Width * scale.X - separation, verticalOffset);
            transform = new Transform2D(position, 0, scale, 
                Vector2.Zero, /*new Vector2(texture.Width/2.0f, texture.Height/2.0f),*/
                new Integer2(texture.Width, texture.Height));

            textureObject = new UITextureObject(AppData.PlayerOneProgressID,
                    ActorType.UITexture,
                    StatusType.Drawn | StatusType.Update,
                    transform, Color.Green,
                    SpriteEffects.None,
                    1,
                    texture);

            //add a controller which listens for pickupeventdata send when the player (or red box) collects the box on the left
            startValue = 3; //just a random number between 0 and max to demonstrate we can set initial progress value
            textureObject.AttachController(new UIProgressController(AppData.PlayerOneProgressControllerID, ControllerType.UIProgress, startValue, 10, this.eventDispatcher));
            this.uiManager.Add(textureObject);
            #endregion


            #region Player 2 Progress Bar
            position = new Vector2(graphics.PreferredBackBufferWidth / 2.0f + separation, verticalOffset);
            transform = new Transform2D(position, 0, scale, Vector2.Zero, new Integer2(texture.Width, texture.Height));

            textureObject = new UITextureObject(AppData.PlayerTwoProgressID,
                    ActorType.UITexture,
                    StatusType.Drawn | StatusType.Update,
                    transform, 
                    Color.Red,
                    SpriteEffects.None,
                    1,
                    texture);

            //add a controller which listens for pickupeventdata send when the player (or red box) collects the box on the left
            startValue = 7; //just a random number between 0 and max to demonstrate we can set initial progress value
            textureObject.AttachController(new UIProgressController(AppData.PlayerTwoProgressControllerID, ControllerType.UIProgress, startValue, 10, this.eventDispatcher));
            this.uiManager.Add(textureObject);
            #endregion
        }
        #endregion

        #region Effects
        private void InitializeEffects()
        {
            BasicEffect basicEffect = null;
           
            #region For unlit colored primitive objects incl. simple lines and wireframe primitives
            basicEffect = new BasicEffect(graphics.GraphicsDevice);
            basicEffect.TextureEnabled = false;
            basicEffect.VertexColorEnabled = true;
            this.effectDictionary.Add(AppData.UnLitColoredPrimitivesEffectID, new BasicEffectParameters(basicEffect));
            #endregion

            #region For unlit textured primitive objects
            basicEffect = new BasicEffect(graphics.GraphicsDevice);
            basicEffect.TextureEnabled = true;
            basicEffect.VertexColorEnabled = false;
            this.effectDictionary.Add(AppData.UnLitTexturedPrimitivesEffectID, new BasicEffectParameters(basicEffect));
            #endregion

            #region For lit (i.e. normals defined) textured primitive objects
            basicEffect = new BasicEffect(graphics.GraphicsDevice);  
            basicEffect.TextureEnabled = true;
            basicEffect.VertexColorEnabled = false;
            basicEffect.EnableDefaultLighting();
            basicEffect.PreferPerPixelLighting = true;
            this.effectDictionary.Add(AppData.LitTexturedPrimitivesEffectID, new BasicEffectParameters(basicEffect));
            #endregion

        }
        #endregion

        #region Content, Update, Draw        
        protected override void LoadContent()
        {
            //moved to Initialize
            //spriteBatch = new SpriteBatch(GraphicsDevice);

//            #region Add Menu & UI
//            InitializeMenu();
//            AddMenuElements();
//            InitializeUI();
//            AddUIElements();
//            #endregion

//#if DEBUG
//            InitializeDebugTextInfo();
//#endif

        }
        protected override void UnloadContent()
        {
            //formally call garbage collection on all ContentDictionary objects to de-allocate resources from RAM
            this.textureDictionary.Dispose();
            this.fontDictionary.Dispose();
            this.videoDictionary.Dispose();

        }

        protected override void Update(GameTime gameTime)
        {
            //exit using new gamepad manager
            if(this.gamePadManager != null && this.gamePadManager.IsPlayerConnected(PlayerIndex.One) && this.gamePadManager.IsButtonPressed(PlayerIndex.One, Buttons.Back))
                this.Exit();

#if DEMO
            DoDebugToggleDemo();
            DoCameraCycle();
#endif

            base.Update(gameTime);
        }

        private void DoCameraCycle()
        {
            if (this.keyboardManager.IsFirstKeyPress(Keys.F1))
            {
                EventDispatcher.Publish(new EventData(EventActionType.OnCameraCycle, EventCategoryType.Camera));
            }
        }

        private void DoDebugToggleDemo()
        {
            if(this.keyboardManager.IsFirstKeyPress(Keys.F5))
            {
                //toggle the boolean variables directly in the debug drawe to show CDCR surfaces
                this.collisionSkinDebugDrawer.ShowCollisionSkins = !this.collisionSkinDebugDrawer.ShowCollisionSkins;
                this.collisionSkinDebugDrawer.ShowZones = !this.collisionSkinDebugDrawer.ShowZones;
            }

            if (this.keyboardManager.IsFirstKeyPress(Keys.F6))
            {
                //toggle the boolean variables directly in the debug drawer to show the frustum culling bounding spheres
                this.collisionSkinDebugDrawer.ShowFrustumCullingSphere = !this.collisionSkinDebugDrawer.ShowFrustumCullingSphere;
            }

            if (this.keyboardManager.IsFirstKeyPress(Keys.F7))
            {
                //we can turn all debug off 
                EventDispatcher.Publish(new EventData(EventActionType.OnToggleDebug, EventCategoryType.Debug));
            }

        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(GoogleGreenColor);

            // TODO: Add your drawing code here

            base.Draw(gameTime);
        }
        #endregion
    }
}
