namespace Articulate.Options
{
    public class MetaWeblogOptions
    {
        public MetaWeblogOptions()
        {
            ExtractFirstImageAsProperty = true;
        }

        /// <summary>
        /// whether or not to extract the first image found in the markup to be saved as the 'post image' for the blog post,
        /// default is true.
        /// </summary>
        public bool ExtractFirstImageAsProperty { get; set; }

    }
}