// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Bot.Builder;

namespace DadaBot
{
    /// <summary>
    /// This class is created as a Singleton and passed into the IBot-derived constructor.
    ///  - See <see cref="EchoWithCounterBot"/> constructor for how that is injected.
    ///  - See the Startup.cs file for more details on creating the Singleton that gets
    ///    injected into the constructor.
    /// </summary>
    public class DadaBotAccessors
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// Contains the <see cref="ConversationState"/> and associated <see cref="IStatePropertyAccessor{T}"/>.
        /// </summary>
        /// <param name="conversationState">The state object that stores the counter.</param>
        public DadaBotAccessors(Microsoft.Bot.Builder.ConversationState conversationState)
        {
            ConversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
        }

        /// <summary>
        /// Gets the <see cref="IStatePropertyAccessor{T}"/> name used for the <see cref="CounterState"/> accessor.
        /// </summary>
        /// <remarks>Accessors require a unique name.</remarks>
        /// <value>The accessor name for the counter accessor.</value>
        public static string CounterStateName { get; } = $"{nameof(DadaBotAccessors)}.CounterState";
        public static string ConversationDataName { get; } = $"{nameof(DadaBotAccessors)}.ConversationData";
        public static string ConversationFlowName { get; } = $"{nameof(DadaBotAccessors)}.ConversationFlow";
        public static string UserProfileName { get; } = $"{nameof(DadaBotAccessors)}.UserProfile";

        /// <summary>
        /// Gets or sets the <see cref="IStatePropertyAccessor{T}"/> for CounterState.
        /// </summary>
        /// <value>
        /// The accessor stores the turn count for the conversation.
        /// </value>
        public IStatePropertyAccessor<CounterState> CounterState { get; set; }

        public IStatePropertyAccessor<ConversationData> ConversationData { get; set; }

        public IStatePropertyAccessor<ConversationFlow> ConversationFlow { get; set; }
        public IStatePropertyAccessor<UserProfile> UserProfile { get; set; }

        /// <summary>
        /// Gets the <see cref="ConversationState"/> object for the conversation.
        /// </summary>
        /// <value>The <see cref="ConversationState"/> object.</value>
        public ConversationState ConversationState { get; }
    }
}
