﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.EntityModel.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Linq;
    using System.IO;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.Text.RegularExpressions;
    using System.Threading;

    using Newtonsoft.Json;

    using Microsoft.DocAsCode.Common;
    using Microsoft.DocAsCode.EntityModel.ViewModels;
    using Microsoft.DocAsCode.EntityModel.Swagger;
    using Microsoft.DocAsCode.Plugins;
    using Microsoft.DocAsCode.Utility;

    [Export(typeof(IDocumentProcessor))]
    public class RestApiDocumentProcessor : DisposableDocumentProcessor
    {
        private const string RestApiDocumentType = "RestApi";
        private const string DocumentTypeKey = "documentType";

        /// <summary>
        /// TODO: resolve JSON reference $ref
        /// </summary>
        private static readonly ThreadLocal<JsonSerializer> _serializer = new ThreadLocal<JsonSerializer>(
            () =>
            {
                var jsonSerializer = new JsonSerializer();
                jsonSerializer.NullValueHandling = NullValueHandling.Ignore;
                jsonSerializer.ReferenceLoopHandling = ReferenceLoopHandling.Serialize;

                // Newtonsoft JSON Reference object does not allow additional content together with $ref however swagger allows:
                // "schema": {
                //           "$ref": "#/definitions/contact",
                //           "example": {
                //               "department": "Sales",
                //               "jobTitle": "Sales Rep"
                //           }
                //       }
                jsonSerializer.MetadataPropertyHandling = MetadataPropertyHandling.Ignore;
                return jsonSerializer;
            });

        [ImportMany(nameof(RestApiDocumentProcessor))]
        public override IEnumerable<IDocumentBuildStep> BuildSteps { get; set; }

        public override string Name => nameof(RestApiDocumentProcessor);

        /// <summary>
        /// TODO: override document
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public override ProcessingPriority GetProcessingPriority(FileAndType file)
        {
            switch (file.Type)
            {
                case DocumentType.Article:
                    if (file.File.EndsWith("_swagger2.json", StringComparison.OrdinalIgnoreCase) ||
                        file.File.EndsWith("_swagger.json", StringComparison.OrdinalIgnoreCase) ||
                        file.File.EndsWith(".swagger.json", StringComparison.OrdinalIgnoreCase) ||
                        file.File.EndsWith(".swagger2.json", StringComparison.OrdinalIgnoreCase))
                    {
                        return ProcessingPriority.Normal;
                    }
                    break;
                case DocumentType.Override:
                    if (".md".Equals(Path.GetExtension(file.File), StringComparison.OrdinalIgnoreCase))
                    {
                        return ProcessingPriority.Normal;
                    }
                    break;
                default:
                    break;
            }
            return ProcessingPriority.NotSupportted;
        }

        public override FileModel Load(FileAndType file, ImmutableDictionary<string, object> metadata)
        {
            switch (file.Type)
            {
                case DocumentType.Article:
                    var filePath = Path.Combine(file.BaseDir, file.File);
                    var swaggerContent = File.ReadAllText(filePath);
                    var swagger = GetModelWithoutRef<SwaggerModel>(swaggerContent);
                    swagger.Metadata[DocumentTypeKey] = RestApiDocumentType;
                    swagger.Raw = swaggerContent;
                    var repoInfo = GitUtility.GetGitDetail(filePath);
                    if (repoInfo != null)
                    {
                        swagger.Metadata["source"] = new SourceDetail() { Remote = repoInfo };
                    }

                    swagger.Metadata = MergeMetadata(swagger.Metadata, metadata);
                    var vm = RestApiItemViewModel.FromSwaggerModel(swagger);
                    var displayLocalPath = repoInfo?.RelativePath ?? Path.Combine(file.BaseDir, file.File).ToDisplayPath();
                    return new FileModel(file, vm, serializer: new BinaryFormatter())
                    {
                        Uids = new UidDefinition[] { new UidDefinition(vm.Uid, displayLocalPath) }.Concat(from item in vm.Children select new UidDefinition(item.Uid, displayLocalPath)).ToImmutableArray(),
                        LocalPathFromRepoRoot = displayLocalPath,
                        Properties =
                        {
                            LinkToFiles = new HashSet<string>(),
                            LinkToUids = new HashSet<string>(),
                        },
                    };
                case DocumentType.Override:
                    var overrides = MarkdownReader.ReadMarkdownAsOverride<RestApiItemViewModel>(file.BaseDir, file.File);
                    if (overrides == null || overrides.Count == 0) return null;

                    displayLocalPath = overrides[0].Documentation?.Remote?.RelativePath ?? Path.Combine(file.BaseDir, file.File).ToDisplayPath();
                    return new FileModel(file, overrides, serializer: new BinaryFormatter())
                    {
                        Uids = (from item in overrides
                                select new UidDefinition(
                                    item.Uid,
                                    displayLocalPath,
                                    item.Documentation.StartLine + 1
                                    )).ToImmutableArray(),
                        Properties =
                        {
                            LinkToFiles = new HashSet<string>(),
                            LinkToUids = new HashSet<string>(),
                        },
                        LocalPathFromRepoRoot = displayLocalPath,
                    };
                default:
                    throw new NotSupportedException();
            }
        }

        public override SaveResult Save(FileModel model)
        {
            if (model.Type != DocumentType.Article)
            {
                throw new NotSupportedException();
            }
            var vm = (RestApiItemViewModel)model.Content;
            string documentType = null;
            object documentTypeObject;
            if (vm.Metadata.TryGetValue(DocumentTypeKey, out documentTypeObject))
            {
                documentType = documentTypeObject as string;
            }
            return new SaveResult
            {
                DocumentType = documentType ?? RestApiDocumentType,
                ModelFile = model.File,
                LinkToFiles = ((HashSet<string>)model.Properties.LinkToFiles).ToImmutableArray(),
                LinkToUids = ((HashSet<string>)model.Properties.LinkToUids).ToImmutableHashSet(),
            };
        }

        internal static T GetModelWithoutRef<T>(string content)
        {
            using(var sr = new StringReader(content))
            {
                return JsonUtility.Deserialize<T>(sr, _serializer.Value);
            }
        }

        #region Private methods

        private static Dictionary<string, object> MergeMetadata(IDictionary<string, object> item, IDictionary<string, object> overrideItem)
        {
            var result = new Dictionary<string, object>(item);
            foreach (var pair in overrideItem)
            {
                if (result.ContainsKey(pair.Key))
                {
                    Logger.LogWarning($"Metadata \"{pair.Key}\" inside rest api is overridden.");
                }

                result[pair.Key] = pair.Value;
            }
            return result;
        }

        #endregion
    }
}
