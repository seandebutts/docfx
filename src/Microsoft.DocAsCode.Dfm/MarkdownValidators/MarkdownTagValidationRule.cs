﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DocAsCode.Dfm.MarkdownValidators
{
    using System.Collections.Generic;

    using Newtonsoft.Json;

    public class MarkdownTagValidationRule
    {
        /// <summary>
        /// The names of tag.
        /// </summary>
        [JsonProperty("tagNames")]
        public List<string> TagNames { get; set; }
        /// <summary>
        /// Define tag's behavior.
        /// </summary>
        [JsonProperty("behavior")]
        public TagValidationBehavior Behavior { get; set; }
        /// <summary>
        /// The message formatter for warning and error. '{0}' is name of tag, '{1}' is the full tag.
        /// </summary>
        [JsonProperty("messageFormatter")]
        public string MessageFormatter { get; set; }
        /// <summary>
        /// The contract name for custom validator <see cref="Microsoft.DocAsCode.Plugins.ICustomMarkdownTagValidator"/>.
        /// </summary>
        [JsonProperty("customValidatorContractName")]
        public string CustomValidatorContractName { get; set; }
        /// <summary>
        /// Only validate opening tag.
        /// </summary>
        [JsonProperty("openingTagOnly")]
        public bool OpeningTagOnly { get; set; }
    }
}
