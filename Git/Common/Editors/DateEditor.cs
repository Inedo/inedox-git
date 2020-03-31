using System.Reflection;
using Inedo.Web.Controls;
using Inedo.Web.Editors.PropertyEditors;

namespace Inedo.Extensions.Editors
{
    public sealed class DateEditor : PropertyEditor
    {
        private new SimpleInput EditorControl => (SimpleInput)base.EditorControl;

        public DateEditor(PropertyInfo property) : base(property)
        {
            this.EditorControl.Attributes["type"] = "date";
            this.EditorControl.Attributes.Remove("autocomplete");
        }

        protected override void BindToControl(object instance)
        {
            this.EditorControl.Value = this.Property.GetValue(instance) as string;
        }

        protected override string GetRawValue()
        {
            return this.EditorControl.Value;
        }

        protected override void WriteToInstance(object instance)
        {
            this.Property.SetValue(instance, AH.NullIf(this.EditorControl.Value, string.Empty));
        }
    }
}
