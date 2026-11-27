using EchoStem.Core.Utils;
using EchoStem.Data;
using EchoStem.Services.Utils;
using Prism.Mvvm;
using System;

namespace EchoStem.Services.Entities
{
    public class GenreViewModel : BindableBase, ISemanticZoomable
    {
        private string genreName;
        private bool isHeader;

        public GenreViewModel(string genreName)
        {
            this.genreName = DataUtils.TrimColumnValue(genreName);
            this.isHeader = false;
        }
      
        public string GenreName
        {
            get { return this.genreName; }
            set
            {
                SetProperty<string>(ref this.genreName, value);
            }
        }

        public string SortGenreName => FormatUtils.GetSortableString(this.genreName, true);

        public string Header => SemanticZoomUtils.GetGroupHeader(this.genreName);

        public bool IsHeader
        {
            get { return this.isHeader; }
            set { SetProperty<bool>(ref this.isHeader, value); }
        }
     
        public override string ToString()
        {
            return this.genreName;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !GetType().Equals(obj.GetType()))
            {
                return false;
            }

            return string.Equals(this.genreName, ((GenreViewModel)obj).genreName, StringComparison.CurrentCultureIgnoreCase);
        }

        public override int GetHashCode()
        {
            return this.genreName.GetHashCode();
        }
    }
}
