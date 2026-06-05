using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class CommitToNewBranch : Popup
    {
        [Required(ErrorMessage = "Branch name is required!")]
        [RegularExpression(@"^[\w\-/\.#\+]+$", ErrorMessage = "Bad branch name format!")]
        [CustomValidation(typeof(CommitToNewBranch), nameof(ValidateBranchName))]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        public Models.Branch BasedOn
        {
            get;
        }

        public CommitToNewBranch(WorkingCopy workingCopy)
        {
            _workingCopy = workingCopy;
            _repo = workingCopy.Repository;
            BasedOn = _repo.CurrentBranch;
        }

        public static ValidationResult ValidateBranchName(string name, ValidationContext ctx)
        {
            if (ctx.ObjectInstance is CommitToNewBranch creator)
            {
                foreach (var branch in creator._repo.Branches)
                {
                    if (branch.FriendlyName.Equals(name, StringComparison.Ordinal))
                        return new ValidationResult("A branch with same name already exists!");
                }

                return ValidationResult.Success;
            }

            return new ValidationResult("Missing runtime context to create branch!");
        }

        public override async Task<bool> Sure()
        {
            return await _workingCopy.CommitToNewBranchAsync(_name);
        }

        private readonly WorkingCopy _workingCopy = null;
        private readonly Repository _repo = null;
        private string _name = null;
    }
}
