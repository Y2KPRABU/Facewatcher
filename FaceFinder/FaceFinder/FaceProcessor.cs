using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;

namespace FaceFinder
{
    /// <summary>
    /// Creates a PersonGroup containing people having one or more associated faces.
    /// Processes faces in images to find matches for a specified person.
    /// Dependencies: Face service.
    /// </summary>
    class FaceProcessor : ViewModelBase
    {
        private readonly IFaceClient faceClient;

        private const string PERSONGROUPID = "ff-person-group-id";
        private readonly Person emptyPerson = new Person(Guid.Empty, string.Empty);

        // Set in CreatePersonAsync()
        private Person NewPersonCreated;

        public IList<Person> RegisteredPersonsList;
        // A trained PersonGroup has at least 1 added face for the specifed person
        // and has successfully completed the training process at least once.
        private bool isPersonGroupTrained;
        public bool IsPersonGroupTrained
        {
            get => isPersonGroupTrained;
            set => SetProperty(ref isPersonGroupTrained, value);
        }

        public FaceProcessor(IFaceClient faceClient)
        {
            this.faceClient = faceClient;
           
        }

        /// <summary>
        /// Returns all faces detected in an image stream
        /// </summary>
        /// <param name="stream">An image</param>
        /// <returns>A list of detected faces or an empty list</returns>
        public async Task<IList<DetectedFace>> GetFaceListAsync(FileStream stream)
        {
            try
            {
                return await faceClient.Face.DetectWithStreamAsync(stream, true, false,
                    new FaceAttributeType[]
                        { FaceAttributeType.Glasses, FaceAttributeType.Smile});
            }
            catch (APIErrorException e)
            {
                Debug.WriteLine("GetFaceListAsync: " + e.Message);
                MessageBox.Show(e.Message, "GetFaceListAsync");
            }
            return Array.Empty<DetectedFace>();
        }

      

        /// <summary>
        /// Returns all Person.Name's associated with PERSONGROUPID
        /// </summary>
        /// <returns>A list of Person.Name's or an empty list</returns>
        public async Task<IList<Person>> GetAllRegdPeopleAsync()
        {
            try
            {
                RegisteredPersonsList = await faceClient.PersonGroupPerson.ListAsync(PERSONGROUPID);
              
            }
            catch (APIErrorException e)
            {
                Debug.WriteLine("GetAllPersonNamesAsync: " + e.Message);
            }
            
            return RegisteredPersonsList;
        }

       

        /// <summary>
        /// creates a Person in PersonGroupPerson
        /// </summary>
        /// <param name="name">PersonGroupPerson.Name</param>
        /// <param name="GroupInfos">A collection specifying the file paths of images associated with <paramref name="name"/></param>
        public async Task<bool> CreatePersonAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) { return false; }
            Debug.WriteLine("GetOrCreatePersonAsync: " + name);

            IsPersonGroupTrained = false;

            string personName = ConfigurePersonName(name);

            if (RegisteredPersonsList != null)
            { // Get Person if it exists.
                foreach (Person person in RegisteredPersonsList)
                {
                    if (person.Name.Equals(personName))
                    {

                        return false;
                    }
                }
            }
            try {
                // await faceClient.PersonGroup.CreateAsync(PERSONGROUPID, personName,String.Empty);
                // Person doesn't exist, create it.
                NewPersonCreated= await faceClient.PersonGroupPerson.CreateAsync(PERSONGROUPID, personName);
                return true;
            
            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("GetOrCreatePersonAsync: " + ae.Message);
                return false;            }
        }

        // Each image should contain only 1 detected face; otherwise, must specify face rectangle.
        /// <summary>
        /// Adds PersistedFace's to 'personName'
        /// </summary>
        /// <param name="selectedItems">A collection specifying the file paths of images to be associated with searchedForPerson</param>
        /// <param name="GroupInfos"></param>
        public async Task AddFacesToPersonAsync(
            IList<ImageInfo> selectedItems, ObservableCollection<ImageInfo> GroupInfos)
        {
               foreach (ImageInfo info in selectedItems)
            {
                string imagePath = info.FilePath;

                // Check for duplicate images

                using (FileStream stream = new FileStream(info.FilePath, FileMode.Open, FileAccess.Read,FileShare.Read))
                {
                    PersistedFace persistedFace =
                        await faceClient.PersonGroupPerson.AddFaceFromStreamAsync(
                            PERSONGROUPID, NewPersonCreated.PersonId, stream, imagePath);
                }

                GroupInfos.Add(info);
            }

        
          
                IsPersonGroupTrained = false;
           
            // Do Training and wait for training complete
             await faceClient.PersonGroup.TrainAsync(PERSONGROUPID);

            IsPersonGroupTrained = await GetTrainingStatusAsync();
        }

        /// <summary>
        /// Determines whether a given face matches searchedForPerson 
        /// </summary>
        /// <param name="faceId">PersistedFace.PersistedFaceId</param>
        /// <param name="newImage">On success, contains confidence value</param>
        /// <returns>Whether <paramref name="faceId"/> matches personToCompare</returns>
        public async Task<bool> MatchFaceAsync(Guid faceId, ImageInfo newImage, Person personToCompare)
        {
            if((faceId == Guid.Empty) || (personToCompare?.PersonId == null)) { return false; }

            VerifyResult results;
            try
            {
                results = await faceClient.Face.VerifyFaceToPersonAsync(
                    faceId, personToCompare.PersonId, PERSONGROUPID);
                newImage.Confidence = results.Confidence.ToString("P") + "- with " + personToCompare.Name;

            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("MatchFaceAsync: " + ae.Message);
                newImage.Confidence = "No Match with" + personToCompare.Name;
                return false;
            }

            // Default: True if similarity confidence is greater than or equal to 0.5.
            // Can change by specifying VerifyResult.Confidence.
            return results.IsIdentical;
        }

 

        /// <summary>
        /// Deletes searchedForPerson
        /// </summary>
        /// <param name="GroupInfos"></param>
        /// <param name="GroupNames"></param>
        /// <param name="askFirst">true to display a confirmation dialog</param>
        public async Task DeletePersonAsync(
            string NameofPersonToRemove, bool askFirst = true)
        {
            MessageBoxResult result;
            try
            {
                result = askFirst ?
                    MessageBox.Show("Delete " + NameofPersonToRemove + " and its training images?",
                        "Delete " + NameofPersonToRemove, MessageBoxButton.OKCancel, MessageBoxImage.Warning) :
                    MessageBoxResult.OK;

                if (result == MessageBoxResult.OK)
                {
                    Person personToDel = null;
                    foreach (Person p in RegisteredPersonsList)
                    {
                        if (p.Name.Equals(NameofPersonToRemove))
                        {
                            personToDel = p; 
                            break;
                        }
                    }
                    await faceClient.PersonGroupPerson.DeleteAsync(PERSONGROUPID, personToDel.PersonId);
                    if (RegisteredPersonsList.Contains(personToDel))
                    {
                        RegisteredPersonsList.Remove(personToDel);
                        Debug.WriteLine("DeletePersonAsync: " + NameofPersonToRemove);
                    }
                    ;
                }
            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("DeletePersonAsync: " + ae.Message);
            }
            catch (Exception e)
            {
                Debug.WriteLine("DeletePersonAsync: " + e.Message);
            }
        }

        // TODO: add progress indicator
        private async Task<bool> GetTrainingStatusAsync()
        {
            TrainingStatus trainingStatus = null;
            try
            {
                do
                {
                    trainingStatus = await faceClient.PersonGroup.GetTrainingStatusAsync(PERSONGROUPID);
                    await Task.Delay(1000);
                } while (trainingStatus.Status == TrainingStatusType.Running);
            }
            catch (APIErrorException ae)
            {
                Debug.WriteLine("GetTrainingStatusAsync: " + ae.Message);
                MessageBox.Show(ae.Message, "GetTrainingStatusAsync");
                return false;
            }
            return trainingStatus.Status == TrainingStatusType.Succeeded;
        }

        // PersistedFace.UserData stores the associated image file path.
        // Returns the image file paths associated with each PersistedFace
        public async Task<IList<string>> GetFaceImagePathsAsync(Person searchedPerson)
        {
            IList<string> faceImagePaths = new List<string>();

            IList<Guid> persistedFaceIds = searchedPerson.PersistedFaceIds;
            foreach(Guid pfid in persistedFaceIds)
            {
                PersistedFace face = await faceClient.PersonGroupPerson.GetFaceAsync(
                    PERSONGROUPID, searchedPerson.PersonId, pfid);
                if (!string.IsNullOrEmpty(face.UserData))
                {
                    string imagePath = face.UserData;
                    if (File.Exists(imagePath))
                    {
                        faceImagePaths.Add(imagePath);
                        Debug.WriteLine("GetFaceImagePathsAsync: " + imagePath);
                    }
                    else
                    {
                        await faceClient.PersonGroupPerson.DeleteFaceAsync(
                            PERSONGROUPID, searchedPerson.PersonId, pfid);
                        Debug.WriteLine("GetFaceImagePathsAsync, file not found, deleting reference: " + imagePath);
                    }
                }
            }
            return faceImagePaths;
        }

        private string ConfigurePersonName(string name)
        {
            return name.Replace(" ", "_");
        }
    }
}
