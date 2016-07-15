using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CHM2HTML
{
    //菜单类
    public class Menu
    {
        public string Name { get; set; }

        public string Url { get; set; }

        public List<Menu> Children;

        public Menu() {
            this.Children = new List<Menu>();
        }

        public Menu(string name, string url)
        {
            this.Name = name;
            this.Url = url;
            this.Children = new List<Menu>();
        }
    }
}
