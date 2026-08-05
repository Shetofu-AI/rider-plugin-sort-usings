using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.Parts;
using JetBrains.Application.Progress;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Daemon.CSharp.CodeCleanup;
using JetBrains.ReSharper.Feature.Services.CodeCleanup;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.ExtensionsAPI.Tree;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace Shetofu.Rider.CodeCleanup
{
    [CodeCleanupModule(Instantiation.DemandAnyThreadSafe, ModulesBefore = new[] { typeof(OptimizeUsings) })]
    public class SortUsingsByLength : ICodeCleanupModule
    {
        private const string MODULE_NAME = "ShetofuSortUsingsByLength";

        private static readonly CodeCleanupOptionDescriptor<bool> SORT_BY_LENGTH_DESCRIPTOR =
            new CodeCleanupOptionDescriptor<bool>(
                MODULE_NAME,
                CodeCleanupOptionDescriptor.CSharpCategory,
                CodeCleanupOptionDescriptor.OptimizeImportsGroup,
                displayName: "Sort using directives by length");

        private static readonly UsingDirectiveComparer USING_DIRECTIVE_COMPARER = new UsingDirectiveComparer();

        public string Name
        {
            get { return MODULE_NAME; }
        }

        public PsiLanguageType LanguageType
        {
            get { return CSharpLanguage.Instance; }
        }

        public ICollection<CodeCleanupOptionDescriptor> Descriptors
        {
            get { return new CodeCleanupOptionDescriptor[] { SORT_BY_LENGTH_DESCRIPTOR }; }
        }

        public bool IsAvailableOnSelection
        {
            get { return false; }
        }

        public bool IsAvailable(IPsiSourceFile sourceFile)
        {
            return sourceFile.GetPrimaryPsiFile() is ICSharpFile;
        }

        public bool IsAvailable(CodeCleanupProfile profile)
        {
            return profile.GetSetting(SORT_BY_LENGTH_DESCRIPTOR);
        }

        public void SetDefaultSetting(CodeCleanupProfile profile, CodeCleanupService.DefaultProfileType profileType)
        {
            profile.SetSetting(SORT_BY_LENGTH_DESCRIPTOR, profileType == CodeCleanupService.DefaultProfileType.FULL);
        }

        public void Process(
            IPsiSourceFile sourceFile,
            IRangeMarker rangeMarkerOrNull,
            CodeCleanupProfile profile,
            IProgressIndicator progressIndicator,
            IUserDataHolder cache)
        {
            if (!profile.GetSetting(SORT_BY_LENGTH_DESCRIPTOR))
            {
                return;
            }

            ICSharpFile file = sourceFile.GetPrimaryPsiFile() as ICSharpFile;
            if (file == null)
            {
                return;
            }

            // CLI (cleanupcode) does not open a PSI transaction for us, Rider does; nested Execute is a no-op there.
            file.GetPsiServices().Transactions.Execute(MODULE_NAME, () => SortImports(file));
        }

        private static void SortImports(ICSharpFile file)
        {
            IUsingDirective[] imports = file.Imports.ToArray();
            if (imports.Length < 2)
            {
                return;
            }

            IUsingDirective[] sorted = new IUsingDirective[imports.Length];
            Array.Copy(imports, sorted, imports.Length);
            Array.Sort(sorted, USING_DIRECTIVE_COMPARER);

            if (!IsReordered(imports, sorted))
            {
                return;
            }

            IUsingDirective[] clones = new IUsingDirective[sorted.Length];
            for (int index = 0; index < sorted.Length; index++)
            {
                clones[index] = (IUsingDirective) ModificationUtil.CloneNode(sorted[index], KeepClonedNodeAsIs);
            }

            for (int index = 0; index < imports.Length; index++)
            {
                ModificationUtil.ReplaceChild(imports[index], clones[index]);
            }
        }

        private static bool IsReordered(IUsingDirective[] imports, IUsingDirective[] sorted)
        {
            for (int index = 0; index < imports.Length; index++)
            {
                if (!ReferenceEquals(imports[index], sorted[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void KeepClonedNodeAsIs(ITreeNode node)
        {
        }

        private sealed class UsingDirectiveComparer : IComparer<IUsingDirective>
        {
            public int Compare(IUsingDirective first, IUsingDirective second)
            {
                int groupDifference = GetGroup(first) - GetGroup(second);
                if (groupDifference != 0)
                {
                    return groupDifference;
                }

                string firstText = first.GetText();
                string secondText = second.GetText();
                if (firstText.Length != secondText.Length)
                {
                    return firstText.Length - secondText.Length;
                }

                return string.CompareOrdinal(firstText, secondText);
            }

            private static int GetGroup(IUsingDirective directive)
            {
                if (directive.IsGlobal)
                {
                    return 0;
                }

                if (directive.IsAlias)
                {
                    return 2;
                }

                return 1;
            }
        }
    }
}