using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

using Microsoft.Azure.CognitiveServices.Vision.Face.Models;

namespace FaceFinder
{
    /// <summary>
    /// Processes image files to detect faces, attributes, and other info.
    /// Dependencies: ImageProcessor & FaceProcessor.
    /// </summary>
    class FaceFinderVM : ViewModelBase
    {
        // TODO: bind to ui & choose
        private const SearchOption searchOption = SearchOption.TopDirectoryOnly;

        private const string isolatedStorageFile = "FaceFinderStorage.txt";
        private const string thumbnailsFolderName = "FaceThumbnails";

        // Defaults associated with free tier, S0.
        private const string _computerVisionEndpoint =
            "https://sunvision2.cognitiveservices.azure.com/";
        private const string _faceEndpoint =
            "https://sundarface.cognitiveservices.azure.com/";

   
        private CancellationTokenSource cancellationTokenSource;
        private FileInfo[] imageFiles = Array.Empty<FileInfo>();

    #region Bound properties
      
        private string computerVisionKey = "5e78b106c2994cb7864ba96362d2b581";
        public string ComputerVisionKey
        {
            get => computerVisionKey;
            set
            {
                SetProperty(ref computerVisionKey, value);
                //SaveDataToIsolatedStorage();
            }
        }
        private string computerVisionEndpoint = _computerVisionEndpoint;
        public string ComputerVisionEndpoint
        {
            get => computerVisionEndpoint;
            set
            {
                SetProperty(ref computerVisionEndpoint, value);
                //SaveDataToIsolatedStorage();
            }
        }
        private string faceKey = "0fac2e6c22c84b4abb0d7fd3ef45d274";
        public string FaceKey
        {
            get => faceKey;
            set
            {
                SetProperty(ref faceKey, value);
                //SaveDataToIsolatedStorage();
            }
        }
        private string faceEndpoint = _faceEndpoint;
       public string NewPersonName { get; set; }
        public string FaceEndpoint
        {
            get => faceEndpoint;
            set
            {
                SetProperty(ref faceEndpoint, value);
                //SaveDataToIsolatedStorage();
            }
        }

        private int fileCount;
        public int FileCount
        {
            get => fileCount;
            set => SetProperty(ref fileCount, value);
        }
        private int imageCount;
        public int ImageCount
        {
            get => imageCount;
            set => SetProperty(ref imageCount, value);
        }
        private int processingCount;
        public int ProcessingCount
        {
            get => processingCount;
            set => SetProperty(ref processingCount, value);
        }
        private int searchedCount;
        public int SearchedCount
        {
            get => searchedCount;
            set => SetProperty(ref searchedCount, value);
        }
        private int faceImageCount;
        public int FaceImageCount
        {
            get => faceImageCount;
            set => SetProperty(ref faceImageCount, value);
        }
        private int faceCount;
        public int FaceCount
        {
            get => faceCount;
            set => SetProperty(ref faceCount, value);
        }

     

        private string selectedFolder = string.Empty;
        public string SelectedFolder
        {
            get => selectedFolder;
            set
            {
                string selectedFolderName = (new DirectoryInfo(value)).Name;
                SetProperty(ref selectedFolder, selectedFolderName);
            }
        }

        // IsChecked
        private bool isSettingsExpanded;
        public bool IsSettingsExpanded
        {
            get => isSettingsExpanded;
            set => SetProperty(ref isSettingsExpanded, value);
        }

        private bool searchSubfolders;
        public bool SearchSubfolders
        {
            get => searchSubfolders;
            set => SetProperty(ref searchSubfolders, value);
        }

        
      

        private bool isPersonComboBoxOpen;
        public bool IsPersonComboBoxOpen
        {
            get => isPersonComboBoxOpen;
            set
            {
                // value == true onOpen, false onClose
                SetProperty(ref isPersonComboBoxOpen, value);

                // Populates personComboBox.
                if ((value && RegdPeopleNames.Count == 0) || !value)
                {
                    GetNamesCommand.Execute(string.Empty);
                }
            }
        }

        private double minAge = 0, maxAge = 1.0;
        public double MinAge
        {
            get => minAge;
            set => SetProperty(ref minAge, value);
        }
        public double MaxAge
        {
            get => maxAge;
            set => SetProperty(ref maxAge, value);
        }
    #endregion Bound properties

    #region Commands
        private ICommand checkIntrudersCommand;
        public ICommand CheckIntrudersCommand
        {
            get
            {
                return checkIntrudersCommand ??
                    (checkIntrudersCommand = new RelayCommand(
                        p => true, async p => await CheckIntruders()));
            }
        }


        private ICommand selectFolderCommand;
        public ICommand SelectFolderCommand
        {
            get
            {
                return selectFolderCommand ??
                    (selectFolderCommand = new RelayCommand(
                        p => true, p => SelectFolder()));
            }
        }

        private ICommand getNamesCommand;
        public ICommand GetNamesCommand
        {
            get
            {
                return getNamesCommand ??
                    (getNamesCommand = new RelayCommand(
                        p => true, async p => await LoadRegdPeopleAsync()));
            }
        }

        private bool isAddPersonButtonEnabled = true;
      
        private async Task CreatePersonAsync(string person)
        {
           bool personCreated=   await faceProcessor.CreatePersonAsync(person);
            if (personCreated)
            {
                await LoadRegdPeopleAsync();
            }
            else
            {
                MessageBox.Show("User Already exists or Error creating users");
            }
        }

        private bool isDeletePersonButtonEnabled = true;
        private ICommand deletePersonCommand;
        public ICommand DeletePersonCommand
        {
            get
            {
                return deletePersonCommand ?? (deletePersonCommand = new RelayCommand(
                    p => isDeletePersonButtonEnabled, 
                    async p => await DeletePersonAsync(SelPersonName)));
            }
        }
        private async Task DeletePersonAsync(string person)
        {
           
            await faceProcessor.DeletePersonAsync( person, true);
            if (RegdPeopleNames.Contains(person))
            {
                RegdPeopleNames.Remove(person);
            }
        }

        private bool isAddToPersonButtonEnabled = true;

        private ICommand createPersonCommand;
         public ICommand CreatePersonCommand
        {
            get
            {
                return createPersonCommand ?? (createPersonCommand = new RelayCommand(p=>true
                    , async p => await CreatePersonAsync(NewPersonName)));
            }
        }
        private ICommand addToPersonCommand;
        public ICommand AddToPersonCommand
        {
            get
            {
                return addToPersonCommand ?? (addToPersonCommand = new RelayCommand(
                    p => isAddToPersonButtonEnabled,
                    async p => await AddToPersonAsync(p)));
            }
        }

        private bool isFindFacesButtonEnabled;
        private ICommand findFacesCommand;
        public ICommand FindFacesCommand
        {
            get
            {
                return findFacesCommand ?? (findFacesCommand = new RelayCommand(
                    p => isFindFacesButtonEnabled, async p => await FindFacesAsync()));
            }
        }

        private bool isCancelButtonEnabled;
        private ICommand cancelFindFacesCommand;
        public ICommand CancelFindFacesCommand
        {
            get
            {
                return cancelFindFacesCommand ?? (cancelFindFacesCommand = new RelayCommand(
                        p => isCancelButtonEnabled, p => CancelFindFaces()));
            }
        }
    #endregion Commands

        public ImageProcessor imageProcessor { get; set; }
        public FaceProcessor faceProcessor { get; set; }
        /// <summary>
        /// Contains Face Information
        /// </summary>
        public ObservableCollection<ImageInfo> ImagesWithFaces { get; set; }
        public ObservableCollection<ImageInfo> ImagesMatched { get; private set; }
        public ObservableCollection<ImageInfo> ImagesIntruders { get; private set; }
        public ObservableCollection<ImageInfo> RegdPeopleImageInfos { get; set; }
        public ObservableCollection<string> RegdPeopleNames { get; set; }
        /// <summary>
        /// Contains Raw Folder images to scan
        /// </summary>
        public ObservableCollection<MovieData> ImagesToScan { get; set; }

        public FaceFinderVM()
        {
            //GetDataFromIsolatedStorage();
            ImagesWithFaces = new ObservableCollection<ImageInfo>();
            ImagesMatched = new ObservableCollection<ImageInfo>();
            ImagesIntruders = new ObservableCollection<ImageInfo>();

            RegdPeopleImageInfos = new ObservableCollection<ImageInfo>();
            RegdPeopleNames = new ObservableCollection<string>();
            
            ImagesToScan = new ObservableCollection<MovieData>();

            SetupVisionServices();
        }

        private void SetupVisionServices()
        {
            App app = (App)Application.Current;

            app.SetupComputerVisionClient(ComputerVisionKey, ComputerVisionEndpoint);
            imageProcessor = new ImageProcessor(app.computerVisionClient);

            app.SetupFaceClient(FaceKey, FaceEndpoint);
            faceProcessor = new FaceProcessor(app.faceClient);
        }

        private async Task FindFacesAsync()
        {
            if (ComputerVisionKey.Equals(string.Empty) || FaceKey.Equals(string.Empty))
            {
                IsSettingsExpanded = true;
                MessageBox.Show("Enter your subscription key(s) in the dialog",
                    "Missing subscription key(s)", 
                    MessageBoxButton.OKCancel, MessageBoxImage.Asterisk);
                return;
            }

            isFindFacesButtonEnabled = false;

            ImagesWithFaces.Clear();

            isCancelButtonEnabled = true;
            cancellationTokenSource = new CancellationTokenSource();

            await ProcessImageFilesForFacesAsync(imageFiles, cancellationTokenSource.Token);

            cancellationTokenSource.Dispose();
            isCancelButtonEnabled = false;

            isFindFacesButtonEnabled = true;

            // TODO: without this statement, app suspends updating UI until explicit focus change (mouse or key event)
            await Task.Delay(1000);
        }
        private void CancelFindFaces()
        {
            isCancelButtonEnabled = false;
            cancellationTokenSource.Cancel();
        }

        // The root of image processing. Calls all the other image processing methods when a face is detected.
        private async Task ProcessImageFilesForFacesAsync(
            FileInfo[] imageFiles, CancellationToken cancellationToken)
        {
            string thumbnailsFolder = imageFiles[0].DirectoryName +
                Path.DirectorySeparatorChar + thumbnailsFolderName;
            if (!Directory.Exists(thumbnailsFolder))
            {
                Directory.CreateDirectory(thumbnailsFolder);
            }
          
            ProcessingCount = 0;    // # of image files processed
            SearchedCount = 0;      // images containing a face
            FaceImageCount = 0;     // images with a face matching the search criteria
            FaceCount = 0;          // images with a face matching the search criteria and selected person

            IList<DetectedFace> detectedfaceList;
            foreach (FileInfo file in imageFiles)
            {
                if (cancellationToken.IsCancellationRequested) { return; }

                ProcessingCount++;
                try
                {
                    using (FileStream stream = file.OpenRead())
                    {
                        detectedfaceList = await faceProcessor.GetFaceListAsync(stream);
                         await Task.Delay(1000);
                    }
                   
                    // Ignore image files without a detected face
                    if (detectedfaceList.Count > 0)
                    {
                        SearchedCount++;
                        int matchedCount = 0;

                        // Holds info about the currently analyzed image file and detected
                        ImageInfo newImage = new ImageInfo();
                        newImage.FilePath = file.DirectoryName + Path.DirectorySeparatorChar + file.Name;
                        newImage.FileName = file.Name;

                       
                            GetImageMetadata(file, newImage);
                        

                        string attributes = string.Empty;
                      foreach (DetectedFace face in detectedfaceList)
                        {
                            
                          /*  double? age = face.FaceAttributes.Age;
                            string gender = face.FaceAttributes.Gender.ToString();

                            if (searchAge && ((age < MinAge) || (age > MaxAge))) { continue; }
                            if (isMale && !gender.Equals(male)) { continue; }
                            if (isFemale && !gender.Equals(female)) { continue; }
                            attributes += gender + " " + age + "   ";
*/
                            matchedCount++;
                            newImage.FoundFace = face;
                          
                        }

                        // No faces matched search criteria
                        if (matchedCount == 0) { continue; }

                        newImage.Attributes = attributes;
                        FaceImageCount += matchedCount;

                        var tasks = new List<Task>();

                      
                            Task thumbTask = imageProcessor.ProcessImageFileForThumbAsync(file, newImage, thumbnailsFolder);
                            tasks.Add(thumbTask);
                       
                            Task captionTask = imageProcessor.ProcessImageFileForCaptionAsync(file, newImage);
                            tasks.Add(captionTask);
                       
                       //     Task ocrTask = imageProcessor.ProcessImageFileForTextAsync(file, newImage);
                        //    tasks.Add(ocrTask);
                      

                        if (tasks.Count != 0)
                        {
                            await Task.WhenAll(tasks);
                        }

                        ImagesWithFaces.Add(newImage);

                            FaceCount = ImagesWithFaces.Count;
                    }
                }
                // Catch and display Face errors.
                catch (APIErrorException fe)
                {
                    Debug.WriteLine("ProcessImageFilesForFacesAsync, api: " + fe.Message);
                    MessageBox.Show(fe.Message + ": " + file.Name, "ProcessImageFilesForFacesAsync");
                    break;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("ProcessImageFilesForFacesAsync: " + e.Message);
                    MessageBox.Show(e.Message + ": " + file.Name, "ProcessImageFilesForFacesAsync");
                    break;
                }
            }
        }
        /// <summary>
        /// Compares 
        /// </summary>
        /// <returns></returns>
        private async Task CheckIntruders()
        {
            // If match on face, call faceProcessor
           // if (faceProcessor.IsPersonGroupTrained)
            {
                if (faceProcessor.RegisteredPersonsList == null)
                    return;
                foreach (Person p in faceProcessor.RegisteredPersonsList)
                { // gets all the faces which was found in FindFaces scan and compares to selected person
                foreach (ImageInfo faceimage in ImagesWithFaces)
                {
                    bool isFaceMatch = await faceProcessor.MatchFaceAsync(
                        (Guid)faceimage.FoundFace.FaceId, faceimage, p);
                    await Task.Delay(500);
                        if (!isFaceMatch)
                        { ImagesIntruders.Add(faceimage); }
                        else
                        { ImagesMatched.Add(faceimage); }

                }
                }
            }
        }
        /// <summary>
        /// Local File Properties
        /// </summary>
        /// <param name="file"></param>
        /// <param name="newImage"></param>
        /// <returns></returns>
        private string GetImageMetadata(FileInfo file, ImageInfo newImage)
        {

            var dateTaken = string.Empty;
            var title = string.Empty;

            try
            {
                using (FileStream fileStream = file.OpenRead())
                {
                    BitmapFrame bitmapFrame = BitmapFrame.Create(
                        fileStream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
                    BitmapMetadata bitmapMetadata = bitmapFrame.Metadata as BitmapMetadata;
                    if (bitmapMetadata?.DateTaken != null)
                    {
                        dateTaken = bitmapMetadata?.DateTaken;
                    }
                    // Throws NotSupportedException on png's (bitmap codec does not support the bitmap property)
                    if (bitmapMetadata?.Title != null)
                    {
                        title = bitmapMetadata?.Title;
                    }
                    
                }
            }
            catch (NotSupportedException e)
            {
                Debug.WriteLine("GetImageMetadata: " + file.Name + "\t" + e.Message);
            }

            var metadata = dateTaken + " " + title;
            if (metadata.Equals(" ")) { metadata = string.Empty; }

            newImage.Metadata = metadata;
            return metadata;
        }

        // Called by IsPersonComboBoxOpen setter
        private async Task LoadRegdPeopleAsync()
        {
            if (RegdPeopleNames.Count > 0)
                return;
            // Wait 2 seconds
            await Task.Delay(2000);
           
                 IList<Person> personNames = await faceProcessor.GetAllRegdPeopleAsync();
            if (personNames == null)
            {
                MessageBox.Show("No Registered People yet");
                return;
            }
            foreach (Person person in personNames)
            {
                if (!RegdPeopleNames.Contains(person.Name))
                {
                    RegdPeopleNames.Add(person.Name);
                    IList<string> facePaths= await faceProcessor.GetFaceImagePathsAsync(person);
                    if (facePaths == null)

                    {
                        if (string.IsNullOrEmpty(person.UserData))
                            continue;
                        else
                        {
                            ImageInfo groupInfo = new ImageInfo();
                            groupInfo.FilePath = person.UserData;
                            RegdPeopleImageInfos.Add(groupInfo);
                        }
                    }
                    else
                        // Get all facepaths returned for the person in above API
                        foreach (string facePath in facePaths)
                        {
                            ImageInfo groupInfo = new ImageInfo(); 
                            groupInfo.FilePath = facePath;
                            RegdPeopleImageInfos.Add(groupInfo);
                        }
                        
                    
                }
            }
        }
        /// <summary>
        /// Adds Faces to Selected Person
        /// </summary>
        /// <param name="selectedThumbnails"></param>
        /// <returns></returns>
        private async Task AddToPersonAsync(object selectedThumbnails)
        {
            if (string.IsNullOrWhiteSpace(NewPersonName)) { return; }

            IList selectedItems = (IList)selectedThumbnails;
            if(selectedItems.Count == 0) { return; }

            IList<ImageInfo> items = selectedItems.Cast<ImageInfo>().ToList();
            await faceProcessor.AddFacesToPersonAsync(items, RegdPeopleImageInfos);
        }


        #region ImageUploadfunctions

        private void SelectFolder()
        {
            string folderPath = PickFolder();
            if (folderPath == string.Empty) { return; }

            SelectedFolder = folderPath;

            imageFiles = GetImageFiles(folderPath);
            if (imageFiles.Length == 0)
            {
                isFindFacesButtonEnabled = false;
                return;
            }
            
       
            isFindFacesButtonEnabled = true;
        }

        // Windows.Forms.FolderBrowserDialog doesn't allow setting the
        // initial view to a specific folder, only an Environment.SpecialFolder.
        private string PickFolder()
        {
            using (var folderPicker = new System.Windows.Forms.FolderBrowserDialog())
            {
                string folderPath = string.Empty;

                folderPicker.Description = "Face Finder";
                folderPicker.RootFolder = Environment.SpecialFolder.MyComputer;
                folderPicker.ShowNewFolderButton = false;

                var dialogResult = folderPicker.ShowDialog();
                if (dialogResult == System.Windows.Forms.DialogResult.OK)
                {
                    folderPath = folderPicker.SelectedPath;
                }
                return folderPath;
            }
        }
        public class MovieData
        {
            private string _Title;
            public string Title
            {
                get { return this._Title; }
                set { this._Title = value; }
            }

            private BitmapImage _ImageData;
            public BitmapImage ImageData
            {
                get { return this._ImageData; }
                set { this._ImageData = value; }
            }

        }
        private FileInfo[] GetImageFiles(string folder)
        {
            DirectoryInfo di = new DirectoryInfo(folder);
            FileCount = di.GetFiles("*.*", searchOption).Length;

            FileInfo[] bmpFiles = di.GetFiles("*.bmp", searchOption);
            FileInfo[] gifFiles = di.GetFiles("*.gif", searchOption);
            FileInfo[] jpgFiles = di.GetFiles("*.jpg", searchOption);
            FileInfo[] pngFiles = di.GetFiles("*.png", searchOption);
            FileInfo[] allImageFiles =
                bmpFiles.Concat(gifFiles).Concat(jpgFiles).Concat(pngFiles).ToArray();

            ImageCount = allImageFiles.Length;
            foreach (FileInfo file in allImageFiles)
            {
                MovieData newImage = new MovieData();
                newImage.ImageData = new BitmapImage(new Uri( file.DirectoryName + Path.DirectorySeparatorChar + file.Name));
                newImage.Title = file.Name;

                ImagesToScan.Add(newImage);
            }
            return allImageFiles;
        }

        private string selPersonName;

        public string SelPersonName { get => selPersonName; set => SetProperty(ref selPersonName, value); }
        #endregion
    }
}
