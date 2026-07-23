// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #pragma  once
// #include  "Utils.h"

using System.ComponentModel;

namespace CalculatorApp.ViewModel.Common
{
    public partial class DisplayExpressionToken(string token, int tokenPosition, bool fEditable, TokenType type)
        : INotifyPropertyChanged
    {
        private string m_Token = token;
        private int m_TokenPosition = tokenPosition;
        private bool m_IsTokenEditable = fEditable;
        private int m_CommandIndex;
        private TokenType m_Type = type;
        private string m_OriginalToken = token;
        private bool m_InEditMode;
        public event PropertyChangedEventHandler? PropertyChanged;
        public string Token
        {
            get => m_Token;
            set
            {
                if (m_Token != value)
                {
                    m_Token = value;
                    OnPropertyChanged(nameof(Token));
                }
            }
        }

        public int TokenPosition
        {
            get => m_TokenPosition;
            set
            {
                if (m_TokenPosition != value)
                {
                    m_TokenPosition = value;
                    OnPropertyChanged(nameof(TokenPosition));
                }
            }
        }

        public bool IsTokenEditable
        {
            get => m_IsTokenEditable;
            set
            {
                if (m_IsTokenEditable != value)
                {
                    m_IsTokenEditable = value;
                    OnPropertyChanged(nameof(IsTokenEditable));
                }
            }
        }

        public int CommandIndex
        {
            get => m_CommandIndex;
            set
            {
                if (m_CommandIndex != value)
                {
                    m_CommandIndex = value;
                    OnPropertyChanged(nameof(CommandIndex));
                }
            }
        }

        public TokenType Type
        {
            get => m_Type;
            set
            {
                if (m_Type != value)
                {
                    m_Type = value;
                    OnPropertyChanged(nameof(Type));
                }
            }
        }

        public string OriginalToken => m_OriginalToken;

        public bool IsTokenInEditMode
        {
            get => m_InEditMode;
            set
            {
                if (m_InEditMode != value)
                {
                    if (!value)
                    {
                        m_OriginalToken = new string(m_Token.ToCharArray());
                    }

                    m_InEditMode = value;
                    OnPropertyChanged(nameof(IsTokenInEditMode));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
