using CommunityToolkit.Mvvm.ComponentModel;
using MusicWrap.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.UI.ViewModels
{
    public partial class PlayerViewModel : ObservableObject
    {
        private readonly IMusicPlayerService _playerService;

        public PlayerViewModel(IMusicPlayerService service)
        {
            _playerService = service;
        }

    }
}
